using NavigationDJIA.World;
using QMind.Interfaces;
using UnityEngine;
using System.IO;
using System.Globalization;

namespace GrupoG
{
    public class QMindTester : IQMind
    {
        private TablaQLearning _brain; // Tabla con lo aprendido
        private WorldInfo _cachedWorld; 
        private const string FILE_PATH = "Assets/Scripts/GrupoG/TablaQ.csv"; // Ruta de la tablaQ.csv

        // Inicializamos:
        public void Initialize(WorldInfo worldInfo)
        {
            _cachedWorld = worldInfo;
            _brain = new TablaQLearning(4, 144); // La tabla con 4 acciones, 144 estados
            ImportKnowledge(); // Se importan los valores 
            Debug.Log("[Tester] Cerebro cargado y listo para inferencia.");
        }

        public CellInfo GetNextStep(CellInfo currentPos, CellInfo enemyPos)
        {
            if (currentPos == null || enemyPos == null) return currentPos;

            // Se calcula el estado actual:
            int stateID = ComputeStateID(currentPos, enemyPos);

            // Se elige la mejor acción según la tabla:
            int bestAction = _brain.GetBestAction(stateID);

            // Se convierte la acción a movimiento:
            return MoveUtils.GetAgentNextStep(bestAction, currentPos, _cachedWorld);
        }

        // Para calcular el ID del estado:
        private int ComputeStateID(CellInfo agent, CellInfo enemy)
        {
            // Se calcula la posición relativa:
            int relX = Mathf.Clamp(enemy.x - agent.x, -1, 1) + 1;
            int relY = Mathf.Clamp(enemy.y - agent.y, -1, 1) + 1;
            int relativeIndex = relX * 3 + relY;

            // Entorno:
            int environmentMask = 0;
            if (CheckWalkable(agent.x, agent.y + 1)) environmentMask |= 1;
            if (CheckWalkable(agent.x + 1, agent.y)) environmentMask |= 2;
            if (CheckWalkable(agent.x, agent.y - 1)) environmentMask |= 4;
            if (CheckWalkable(agent.x - 1, agent.y)) environmentMask |= 8;

            return (relativeIndex * 16) + environmentMask;
        }

        // Booleano que comprueba si la celdaes caminable:
        private bool CheckWalkable(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _cachedWorld.WorldSize.x || y >= _cachedWorld.WorldSize.y) return false;
            return _cachedWorld[x, y].Walkable;
        }

        // Cargamos la tabla desde el csv:
        private void ImportKnowledge()
        {
            if (!File.Exists(FILE_PATH))
            {
                Debug.LogError($"[Tester] ¡No se encuentra el archivo {FILE_PATH}!");
                return;
            }

            var lines = File.ReadAllLines(FILE_PATH);
            foreach (var line in lines)
            {
                if (char.IsDigit(line[0])) // Para saltar cabeceras
                {
                    var p = line.Split(',');
                    if (p.Length == 3)
                    {
                        _brain.SetValue(
                            int.Parse(p[0]), // Estado
                            int.Parse(p[1]), // Acción
                            float.Parse(p[2], CultureInfo.InvariantCulture) // El valor Q
                        );
                    }
                }
            }
        }
    }
}