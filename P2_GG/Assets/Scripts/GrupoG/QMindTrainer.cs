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
    public class QMindTrainer : IQMindTrainer
    {
        // Constantes y Configuración
        private const string CSV_PATH = "Assets/Scripts/GrupoG/TablaQ.csv"; 
        private const int NUM_ACTIONS = 4;
        private const int NUM_STATES = 144;

        // Variables de estado
        private TablaQLearning _qTable;
        private QMindTrainerParams _params;
        private WorldInfo _world;
        private INavigationAlgorithm _enemyNav;

        private float _accumulatedReward;
        private float _avgReturn;

        // Propiedades de la Interfaz
        public int CurrentEpisode { get; private set; }
        public int CurrentStep { get; private set; }
        public CellInfo AgentPosition { get; private set; }
        public CellInfo OtherPosition { get; private set; }
        public float Return => _accumulatedReward;
        public float ReturnAveraged => _avgReturn;

        public event EventHandler OnEpisodeStarted;
        public event EventHandler OnEpisodeFinished;

        public void Initialize(QMindTrainerParams trainerParams, WorldInfo worldInfo, INavigationAlgorithm navAlgo)
        {
            Debug.Log("[Trainer] Inicializando sistema Q-Learning...");
            Time.timeScale = 10f; // Aceleramos el entrenamiento

            _params = trainerParams;
            _world = worldInfo;
            _enemyNav = navAlgo;
            _qTable = new TablaQLearning(NUM_ACTIONS, NUM_STATES);

            // Intentar recuperar entrenamiento previo
            LoadQTable();

            ResetTrainingState();
            StartNewEpisode();
        }

        public void DoStep(bool isTraining)
        {
            // 1. Percibir Estado S
            int currentState = CalculateState(AgentPosition, OtherPosition);

            // 2. Elegir Acción A (Epsilon-Greedy)
            int action = SelectAction(currentState, isTraining);

            // 3. Ejecutar Acción y observar S'
            CellInfo nextPos = MoveUtils.GetAgentNextStep(action, AgentPosition, _world);
            int nextState = CalculateState(nextPos, OtherPosition);

            // 4. Aprendizaje (solo si train == true)
            if (isTraining)
            {
                float reward = GetReward(nextPos);
                _accumulatedReward += reward;
                Learn(currentState, action, reward, nextState);
            }

            // 5. Actualizar físicas
            AgentPosition = nextPos;
            OtherPosition = MoveUtils.GetEnemyNextStep(_enemyNav, OtherPosition, AgentPosition);

            // 6. Comprobar condiciones de fin de episodio
            bool caught = AgentPosition.Equals(OtherPosition);
            bool timeOut = CurrentStep >= _params.maxSteps;

            if (caught || timeOut)
            {
                OnEpisodeFinished?.Invoke(this, EventArgs.Empty);
                EndEpisode();
            }
            else
            {
                CurrentStep++;
            }
        }

        private void Learn(int state, int action, float reward, int nextState)
        {
            float currentQ = _qTable.GetValue(action, state);
            float maxNextQ = _qTable.GetValue(_qTable.GetBestAction(nextState), nextState); // Q(s', a')

            // Ecuación de Bellman
            float newQ = currentQ + _params.alpha * (reward + (_params.gamma * maxNextQ) - currentQ);

            _qTable.SetValue(action, state, newQ);
        }

        private int SelectAction(int state, bool allowExploration)
        {
            // Exploración: Epsilon Greedy
            if (allowExploration && UnityEngine.Random.value < _params.epsilon)
            {
                return UnityEngine.Random.Range(0, NUM_ACTIONS);
            }
            // Explotación: Mejor valor conocido
            return _qTable.GetBestAction(state);
        }

        // Definición de Estados: 9 Posiciones Relativas * 16 Combinaciones de muros = 144 Estados
        private int CalculateState(CellInfo agent, CellInfo enemy)
        {
            // A. Posición relativa del enemigo (Grid 3x3 centrado en agente)
            int dx = Mathf.Clamp(enemy.x - agent.x, -1, 1) + 1; // 0, 1, 2
            int dy = Mathf.Clamp(enemy.y - agent.y, -1, 1) + 1; // 0, 1, 2
            int relativePos = dx * 3 + dy; // 0 a 8

            // B. Muros alrededor (Norte, Este, Sur, Oeste) -> Bitmask
            int wallsMask = 0;
            if (IsWalkable(agent.x, agent.y + 1)) wallsMask |= 1; // Norte (bit 0)
            if (IsWalkable(agent.x + 1, agent.y)) wallsMask |= 2; // Este (bit 1)
            if (IsWalkable(agent.x, agent.y - 1)) wallsMask |= 4; // Sur (bit 2)
            if (IsWalkable(agent.x - 1, agent.y)) wallsMask |= 8; // Oeste (bit 3)

            return (relativePos * 16) + wallsMask;
        }

        private bool IsWalkable(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _world.WorldSize.x || y >= _world.WorldSize.y) return false;
            return _world[x, y].Walkable;
        }

        private float GetReward(CellInfo pos)
        {
            if (pos.Equals(OtherPosition)) return -100f; // Muerte
            return 1f; // Supervivencia
        }

        private void EndEpisode()
        {
            CurrentEpisode++;

            // Cálculo de media móvil para suavizar la gráfica
            _avgReturn = Mathf.Lerp(_avgReturn, _accumulatedReward, 0.05f);

            // Decaimiento de Epsilon (Linear decay)
            float progress = Mathf.Clamp01((float)CurrentEpisode / _params.episodes);
            if (progress < 0.8f)
                _params.epsilon = Mathf.Lerp(0.8f, 0.1f, progress / 0.8f);

            // Guardado periódico
            if (CurrentEpisode % _params.episodesBetweenSaves == 0)
                SaveQTable();

            StartNewEpisode();
        }

        private void StartNewEpisode()
        {
            _accumulatedReward = 0;
            CurrentStep = 0;

            // Respawn aleatorio
            AgentPosition = _world.RandomCell();
            OtherPosition = _world.RandomCell();

            OnEpisodeStarted?.Invoke(this, EventArgs.Empty);
        }

        private void ResetTrainingState()
        {
            CurrentEpisode = 0;
            _avgReturn = 0;
        }

        // --- Persistencia CSV ---
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