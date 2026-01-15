using System;
using NavigationDJIA.Interfaces;
using NavigationDJIA.World;
using UnityEngine;

namespace GrupoG
{
    public static class MoveUtils
    {
        // Calcula el siguiente paso del enemigo usando el algoritmo de búsqueda
        public static CellInfo GetEnemyNextStep(INavigationAlgorithm navAlgo, CellInfo enemyPos, CellInfo agentPos)
        {
            if (navAlgo == null || enemyPos == null || agentPos == null)
            {
                Debug.LogError("[MoveUtils] Faltan referencias para calcular ruta.");
                return enemyPos;
            }

            try
            {
                // Solicitamos el camino con una profundidad máxima segura
                var path = navAlgo.GetPath(enemyPos, agentPos, 100);

                // Si hay camino, devolvemos el primer paso, si no, se queda quieto
                return (path != null && path.Length > 0) ? path[0] : enemyPos;
            }
            catch (Exception e)
            {
                // En caso de error crítico en el algoritmo de búsqueda, mantenemos posición
                Debug.LogWarning($"[MoveUtils] Excepción en ruta: {e.Message}");
                return enemyPos;
            }
        }

        // Calcula la celda destino del agente según la acción (0-3)
        public static CellInfo GetAgentNextStep(int actionIdx, CellInfo currentPos, WorldInfo world)
        {
            int targetX = currentPos.x;
            int targetY = currentPos.y;

            // Mapeo de acciones: 0:Norte, 1:Este, 2:Sur, 3:Oeste
            switch (actionIdx)
            {
                case 0: targetY++; break;
                case 1: targetX++; break;
                case 2: targetY--; break;
                case 3: targetX--; break;
                default: return currentPos;
            }

            // Comprobamos límites del mundo
            if (targetX < 0 || targetY < 0 || targetX >= world.WorldSize.x || targetY >= world.WorldSize.y)
                return currentPos;

            // Comprobamos si la celda es transitable (no es muro)
            var targetCell = world[targetX, targetY];
            return (targetCell != null && targetCell.Walkable) ? targetCell : currentPos;
        }
    }
}