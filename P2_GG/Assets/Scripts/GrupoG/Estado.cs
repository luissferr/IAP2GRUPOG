using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrupoG
{
    public class Estado
    {
        public int Id;
        public Vector2 PosicionEnemigo;
        public bool[] Direcciones; //Almacenamos la caminabilidad hacia las cuatro direcciones (norte, sur, este y oeste)

        public Estado(int Id, Vector2 PosicionEnemigo, bool[] Direcciones)
        {
            this.Id = Id;
            this.PosicionEnemigo = PosicionEnemigo;
            this.Direcciones = Direcciones;
        }
    }
}

