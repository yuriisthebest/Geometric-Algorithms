﻿namespace Shepherd
{
    using General.Controller;
    using General.Menu;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;
    using Util.Algorithms.DCEL;
    using Util.Geometry;
    using Util.Geometry.DCEL;
    using Util.Geometry.Duality;
    using Util.Geometry.Polygon;

    public class ShepherdController : MonoBehaviour, IController
    {
        [Header("Levels")]
        [SerializeField]
        private List<ShepherdLevel> m_levels;

        [Header("Sheep Prefabs")]
        [SerializeField]
        private GameObject m_sheepPrefab;

        
        private int m_levelCounter = 0;


        private List<GameObject> m_sheep = new List<GameObject>();

        private bool m_levelSolved;
        private bool m_restartLevel;

        private static List<Color> Colors = new List<Color> {Color.red, Color.green, Color.yellow, Color.blue};

        void Start()
        {
            InitLevel();
        }

        void Update()
        {

        }

        public void InitLevel()
        {
            foreach (var sheep in m_sheep) Destroy(sheep);

            m_sheep.Clear();

            m_levelSolved = false;
            m_restartLevel = false;

            var level = m_levels[m_levelCounter];

            for (int i = 0; i < level.SheepList.Count; i++)
            {
                var sheep = level.SheepList[i];
                var type = level.SheepTypes[i];
                var pos = new Vector3(sheep.x, sheep.y, -1);
                var obj = Instantiate(m_sheepPrefab, pos, Quaternion.identity) as GameObject;
                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                sr.color = Colors[type];
                m_sheep.Add(obj);
            }
        }

        public void CheckSolution()
        {
            throw new System.NotImplementedException();
        }

        public void AdvanceLevel()
        {
            m_levelCounter++;
            InitLevel();
        }

        public Polygon2D LocatePoint(Vector2D p, DCEL InGraph) 
        {
            var vc = VertDecomp(InGraph);
        }

        // Vertical decomposition
        public List<Polygon2D> VertDecomp(DCEL InGraph) 
        {
            
        }

        public void DrawVoronoi()
        {
        
        }


    }
}