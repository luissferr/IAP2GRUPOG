using NavigationDJIA.Interfaces;
using System;
using NavigationDJIA.World;
using QMind.Interfaces;
using UnityEngine;
using System.IO;
using System.Globalization;
using QMind;

namespace GrupoG
{
    public class QMindTrainer : IQMindTrainer // Entrenamos al agente
    {
        // Constantes:
        private const string CSV_PATH = "Assets/Scripts/GrupoG/TablaQ.csv"; // Ruta de la tablaQ.csv
        private const int NUM_ACTIONS = 4; // Norte, sur, este y oeste = 4
        private const int NUM_STATES = 144; // 9 Posiciones Relativas * 16 Combinaciones de muros = 144 Estados

        // Variables de estado:
        private TablaQLearning _qTable; // Para almacenar los valores
        private QMindTrainerParams _params;
        private WorldInfo _world;
        private INavigationAlgorithm _enemyNav; // Algoritmo de navegación

        private float _accumulatedReward;
        private float _avgReturn;

        // Propiedades de la interfaz:
        public int CurrentEpisode { get; private set; }
        public int CurrentStep { get; private set; }
        public CellInfo AgentPosition { get; private set; }
        public CellInfo OtherPosition { get; private set; }
        public float Return => _accumulatedReward;
        public float ReturnAveraged => _avgReturn;

        public event EventHandler OnEpisodeStarted;
        public event EventHandler OnEpisodeFinished;

        // Inicializamos:
        public void Initialize(QMindTrainerParams trainerParams, WorldInfo worldInfo, INavigationAlgorithm navAlgo)
        {
            Debug.Log("[Trainer] Inicializando sistema Q-Learning...");
            Time.timeScale = 10f; // Aceleramos el tiempo

            _params = trainerParams;
            _world = worldInfo;
            _enemyNav = navAlgo;
            // Creamos la tabla Q:
            _qTable = new TablaQLearning(NUM_ACTIONS, NUM_STATES);

            // Para intentar recuperar entrenamiento previo:
            LoadQTable();

            ResetTrainingState(); // Reinicio de contadores
            StartNewEpisode(); 
        }

        public void DoStep(bool isTraining)
        {
            // Calculamos el estado actual S:
            int currentState = CalculateState(AgentPosition, OtherPosition);

            // Elegimos una acción A con epsilon-greedy:
            int action = SelectAction(currentState, isTraining);

            // Ejecutamos la acci´on y tenemos nuevo estado S:
            CellInfo nextPos = MoveUtils.GetAgentNextStep(action, AgentPosition, _world);
            int nextState = CalculateState(nextPos, OtherPosition);

            // Aprendizaje (solo si train == true)
            if (isTraining)
            {
                float reward = GetReward(nextPos); // Para calcular la recompensa obtenida
                _accumulatedReward += reward;
                Learn(currentState, action, reward, nextState); // Actualizamos la tabla
            }

            // Actualizamos las posiciones:
            AgentPosition = nextPos;
            OtherPosition = MoveUtils.GetEnemyNextStep(_enemyNav, OtherPosition, AgentPosition);

            // Comprobamos si ha terminado el episodio:
            bool caught = AgentPosition.Equals(OtherPosition);
            bool timeOut = CurrentStep >= _params.maxSteps;

            if (caught || timeOut)
            {
                OnEpisodeFinished?.Invoke(this, EventArgs.Empty);
                EndEpisode();
            }
            else
            {
                CurrentStep++; // Avanza un paso si no ha terminado el episodio
            }
        }

        private void Learn(int state, int action, float reward, int nextState)
        {
            float currentQ = _qTable.GetValue(action, state);
            float maxNextQ = _qTable.GetValue(_qTable.GetBestAction(nextState), nextState);

            // Ecuación de Bellman:
            float newQ = currentQ + _params.alpha * (reward + (_params.gamma * maxNextQ) - currentQ);

            _qTable.SetValue(action, state, newQ); // Nuevo valor Q guardado
        }

        private int SelectAction(int state, bool allowExploration)
        {
            // Exploración: Epsilon Greedy
            if (allowExploration && UnityEngine.Random.value < _params.epsilon)
            {
                return UnityEngine.Random.Range(0, NUM_ACTIONS);
            }
            // Explotación: mejor acción conocida
            return _qTable.GetBestAction(state);
        }

        // Calculamos el estado del entorno: 
        private int CalculateState(CellInfo agent, CellInfo enemy)
        {
            // A. Posición relativa del enemigo:
            int dx = Mathf.Clamp(enemy.x - agent.x, -1, 1) + 1; // 0, 1, 2
            int dy = Mathf.Clamp(enemy.y - agent.y, -1, 1) + 1; // 0, 1, 2
            int relativePos = dx * 3 + dy; // 0 a 8

            // B. Muros alrededor (Norte, Este, Sur, Oeste): Bitmask
            int wallsMask = 0;
            if (IsWalkable(agent.x, agent.y + 1)) wallsMask |= 1; // Norte (bit 0)
            if (IsWalkable(agent.x + 1, agent.y)) wallsMask |= 2; // Este (bit 1)
            if (IsWalkable(agent.x, agent.y - 1)) wallsMask |= 4; // Sur (bit 2)
            if (IsWalkable(agent.x - 1, agent.y)) wallsMask |= 8; // Oeste (bit 3)

            return (relativePos * 16) + wallsMask;
        }

        // Booleano para comprobar si la celda es caminable:
        private bool IsWalkable(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _world.WorldSize.x || y >= _world.WorldSize.y) return false;
            return _world[x, y].Walkable;
        }

        // Obtenemos resompensa:
        private float GetReward(CellInfo pos)
        {
            if (pos.Equals(OtherPosition))
                return -1f;

            return 0.05f; // Recompensa por seguir vivio
        }

        // Fin de un episodio:
        private void EndEpisode()
        {
            CurrentEpisode++;

            // Cálculo de media móvil para suavizar la recompensa:
            _avgReturn = Mathf.Lerp(_avgReturn, _accumulatedReward, 0.05f);

            // Reducción de epsilon (Linear decay):
            float progress = Mathf.Clamp01((float)CurrentEpisode / _params.episodes);
            if (progress < 0.8f)
                _params.epsilon = Mathf.Lerp(0.8f, 0.1f, progress / 0.8f);

            // Guardado de la tabla:
            if (CurrentEpisode % _params.episodesBetweenSaves == 0)
                SaveQTable();

            StartNewEpisode(); // Empezamos un nuevo episodio
        }

        private void StartNewEpisode()
        {
            _accumulatedReward = 0;
            CurrentStep = 0;

            // Posiciones aleatorias para el agente y el enemigo:
            AgentPosition = _world.RandomCell();
            OtherPosition = _world.RandomCell();

            OnEpisodeStarted?.Invoke(this, EventArgs.Empty);
        }

        // Reiniciamos los contadores del entrenamiento:
        private void ResetTrainingState()
        {
            CurrentEpisode = 0;
            _avgReturn = 0;
        }

        // Guardamos la tabla en un archivo csv:
        private void SaveQTable()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(CSV_PATH))
                {
                    sw.WriteLine("State,Action,Value"); // Cabecera estándar
                    for (int s = 0; s < NUM_STATES; s++)
                    {
                        for (int a = 0; a < NUM_ACTIONS; a++)
                        {
                            float val = _qTable.GetValue(a, s);
                            // Usamos InvariantCulture para asegurar punto decimal y no coma
                            sw.WriteLine($"{s},{a},{val.ToString(CultureInfo.InvariantCulture)}");
                        }
                    }
                }
                Debug.Log($"[Trainer] Tabla guardada. Ep: {CurrentEpisode}");
            }
            catch (Exception e) { Debug.LogError($"Error guardando CSV: {e.Message}"); }
        }

        // Para cargar la tabla:
        private void LoadQTable()
        {
            if (!File.Exists(CSV_PATH)) return;

            try
            {
                string[] lines = File.ReadAllLines(CSV_PATH);
                foreach (string line in lines)
                {
                    if (line.StartsWith("State") || string.IsNullOrWhiteSpace(line)) continue;

                    var data = line.Split(',');
                    if (data.Length == 3)
                    {
                        int s = int.Parse(data[0]);
                        int a = int.Parse(data[1]);
                        float v = float.Parse(data[2], CultureInfo.InvariantCulture);
                        _qTable.SetValue(a, s, v);
                    }
                }
                Debug.Log("[Trainer] Tabla cargada correctamente.");
            }
            catch (Exception) { Debug.LogWarning("No se pudo cargar tabla previa o archivo corrupto."); }
        }

        // GUI Debug
        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10, 10, 400, 30), $"Episodio: {CurrentEpisode} | Paso: {CurrentStep}", style);
            GUI.Label(new Rect(10, 30, 400, 30), $"Recompensa (Avg): {_avgReturn:F2}", style);
            GUI.Label(new Rect(10, 50, 400, 30), $"Epsilon: {_params.epsilon:F3}", style);
        }
    }
}