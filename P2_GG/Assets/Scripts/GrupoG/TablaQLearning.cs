using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrupoG
{
    public class TablaQLearning
    {
        private readonly float[,] _qMatrix;
        public readonly int ActionCount;
        public readonly int StateCount;

        public TablaQLearning(int actions, int states)
        {
            ActionCount = actions;
            StateCount = states;
            _qMatrix = new float[ActionCount, StateCount];
        }

        public float GetValue(int action, int state)
        {
            if (IsIndexValid(action, state))
                return _qMatrix[action, state];
            return 0f;
        }

        public void SetValue(int action, int state, float value)
        {
            if (IsIndexValid(action, state))
                _qMatrix[action, state] = value;
        }

        // Selecciona la acción con mayor valor Q. En caso de empate, elige al azar.
        public int GetBestAction(int state)
        {
            float maxVal = float.MinValue;
            List<int> candidates = new List<int>();

            for (int i = 0; i < ActionCount; i++)
            {
                float val = GetValue(i, state);
                if (val > maxVal)
                {
                    maxVal = val;
                    candidates.Clear();
                    candidates.Add(i);
                }
                else if (Mathf.Abs(val - maxVal) < 0.0001f) // Comparación segura de floats
                {
                    candidates.Add(i);
                }
            }

            // Si por algún error no hay candidatos, devolvemos 0
            if (candidates.Count == 0) return 0;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private bool IsIndexValid(int action, int state)
        {
            return action >= 0 && action < ActionCount && state >= 0 && state < StateCount;
        }
    }
}