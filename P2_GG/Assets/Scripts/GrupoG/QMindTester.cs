using NavigationDJIA.World;
using QMind.Interfaces;
using UnityEngine;
using System.IO;
using System.Globalization;

namespace GrupoG
{
    public class QMindTester : IQMind
    {
        private TablaQLearning _brain;
        private WorldInfo _cachedWorld;
        private const string FILE_PATH = "Assets/Scripts/GrupoG/TablaQ.csv";

        public void Initialize(WorldInfo worldInfo)
        {
            _cachedWorld = worldInfo;
            _brain = new TablaQLearning(4, 144); // 4 Acciones, 144 Estados
            ImportKnowledge();
            Debug.Log("[Tester] Cerebro cargado y listo para inferencia.");
        }

        public CellInfo GetNextStep(CellInfo currentPos, CellInfo enemyPos)
        {
            if (currentPos == null || enemyPos == null) return currentPos;

            // 1. Determinar el estado actual (Debe ser idéntico al Trainer)
            int stateID = ComputeStateID(currentPos, enemyPos);

            // 2. Consultar la tabla Q para la mejor acción (Explotación pura)
            int bestAction = _brain.GetBestAction(stateID);

            // 3. Traducir acción a movimiento
            return MoveUtils.GetAgentNextStep(bestAction, currentPos, _cachedWorld);
        }

        private int ComputeStateID(CellInfo agent, CellInfo enemy)
        {
            // Cálculo de posición relativa
            int relX = Mathf.Clamp(enemy.x - agent.x, -1, 1) + 1;
            int relY = Mathf.Clamp(enemy.y - agent.y, -1, 1) + 1;
            int relativeIndex = relX * 3 + relY;

            // Cálculo de entorno (caminabilidad)
            int environmentMask = 0;
            if (CheckWalkable(agent.x, agent.y + 1)) environmentMask |= 1;
            if (CheckWalkable(agent.x + 1, agent.y)) environmentMask |= 2;
            if (CheckWalkable(agent.x, agent.y - 1)) environmentMask |= 4;
            if (CheckWalkable(agent.x - 1, agent.y)) environmentMask |= 8;

            return (relativeIndex * 16) + environmentMask;
        }

        private bool CheckWalkable(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _cachedWorld.WorldSize.x || y >= _cachedWorld.WorldSize.y) return false;
            return _cachedWorld[x, y].Walkable;
        }

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
                if (char.IsDigit(line[0])) // Truco simple para saltar cabeceras
                {
                    var p = line.Split(',');
                    if (p.Length == 3)
                    {
                        _brain.SetValue(
                            int.Parse(p[0]),
                            int.Parse(p[1]),
                            float.Parse(p[2], CultureInfo.InvariantCulture)
                        );
                    }
                }
            }
        }
    }
}