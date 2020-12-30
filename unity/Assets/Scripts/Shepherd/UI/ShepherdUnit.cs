namespace Shepherd
{
    using UnityEngine;

    public class ShepherdUnit : MonoBehaviour
    {
        private ShepherdController m_controller;
        public Vector2 vertex;

        
            


        void Awake()
        {
            // find gamecontroller in scene
            m_controller = FindObjectOfType<ShepherdController>();
            vertex = transform.position;
        }

    }
}