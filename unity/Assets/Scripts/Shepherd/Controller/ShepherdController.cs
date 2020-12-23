namespace Shepherd
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
    using Util.Geometry.Triangulation;
    using Util.Algorithms.Triangulation;
    using Voronoi;

    public class ShepherdController : MonoBehaviour, IController
    {
        [Header("Levels")]
        [SerializeField]
        private List<ShepherdLevel> m_levels;

        [Header("Sheep Prefabs")]
        [SerializeField]
        private GameObject m_sheepPrefab;

        [SerializeField]
        private MeshFilter m_meshFilter;

        
        private int m_levelCounter = 0;


        private List<GameObject> m_sheep = new List<GameObject>();

        private bool m_levelSolved;
        private bool m_restartLevel;

        private static List<Color> Colors = new List<Color> {Color.red, Color.green, Color.yellow, Color.blue};

        public GameObject shepherd;
        private bool cooldown = false;

        private enum EOwnership
        {
            UNOWNED,
            PLAYER1,
            PLAYER2,
            PLAYER3,
            PLAYER4
        }

        private readonly Dictionary<Face, EOwnership> m_ownership = new Dictionary<Face, EOwnership>();
        [SerializeField]
        private int playerIndex;
        
        private DCEL m_dcel;

        private System.Random rd = new System.Random();

        void Start()
        {
            InitLevel();
        }

        void Update()
        {
            // Add a shepherd to the game when the user clicks
            if (Input.GetMouseButton(0) && !cooldown) {
                var mousePos = Input.mousePosition;
                mousePos.z = 2.0f;
                var objectPos = Camera.main.ScreenToWorldPoint(mousePos);
                Instantiate(shepherd, objectPos, Quaternion.identity);
                // Don't let the user spam a lot of shepherds
                Invoke("ResetCooldown", 0.5f);
                cooldown = true;
                
                // Test: draw a square of random color next to placed sheep
                //m_dcel = new DCEL();
                var v1 = m_dcel.AddVertex(new Vector2(objectPos.x, objectPos.y));
                var v2 = m_dcel.AddVertex(new Vector2(objectPos.x + 1, objectPos.y));
                var v3 = m_dcel.AddVertex(new Vector2(objectPos.x + 1, objectPos.y + 1));
                var v4 = m_dcel.AddVertex(new Vector2(objectPos.x, objectPos.y + 1));

                m_dcel.AddEdge(v1, v2);
                m_dcel.AddEdge(v2, v3);
                m_dcel.AddEdge(v3, v4);
                HalfEdge e1 = m_dcel.AddEdge(v4, v1);

                e1.Twin.Face.owner = rd.Next(4);

                UpdateMesh();
            }
        }

        private void ResetCooldown() {
            cooldown = false;
        }

        // Anne
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

            m_dcel = new DCEL();
        }

        // Yuri
        public void CheckSolution()
        {
            throw new System.NotImplementedException();
        }

        public void AdvanceLevel()
        {
            m_levelCounter++;
            InitLevel();
        }

        // Anne
        public Polygon2D LocatePoint(Vector2D p, DCEL InGraph) 
        {
            var vc = VertDecomp(InGraph);
            return null;
        }

        /* Create a vertical decomposition of the Voronoi diagram of the shepherds
         *  will be used to determine the nearest shepherd for each sheep
         * 
         * Responsible: Yuri
         */
        public VerticalDecomposition VertDecomp(DCEL InGraph) 
        {
            return new VerticalDecomposition(InGraph);
        }

        // Christine
        public void CreateVoronoi()
        {
        
        }

        // Anne
        public void DrawVoronoi(DCEL VoronoiGraph)
        {
            
        }

        /// <summary>
        /// Updates the mesh according to the Voronoi DCEL.
        /// (Edited from Voronoi game)
        /// </summary>
        private void UpdateMesh()
        {
            if (m_meshFilter.mesh == null)
            {
                // create initial mesh
                m_meshFilter.mesh = new Mesh
                {
                    subMeshCount = 4
                };
                m_meshFilter.mesh.MarkDynamic();
            }
            else
            {
                // clear old mesh
                m_meshFilter.mesh.Clear();
                m_meshFilter.mesh.subMeshCount = 4;
            }

            // build vertices and triangle list
            var vertices = new List<Vector3>();
            var triangles = new List<int>[4] {
                new List<int>(),
                new List<int>(),
                new List<int>(),
                new List<int>()
            };

            // iterate over vertices and create triangles accordingly
            foreach (var face in m_dcel.Faces)
            {
                playerIndex = face.owner;

                // cant triangulate outer face
                if (face.IsOuter) continue;

                // triangulate face polygon
                var triangulation = Triangulator.Triangulate(face.Polygon.Outside);

                // add triangles to correct list
                foreach (var triangle in triangulation.Triangles)
                {
                    int curCount = vertices.Count;

                    // add triangle vertices
                    vertices.Add(new Vector3(triangle.P0.x, 0, triangle.P0.y));
                    vertices.Add(new Vector3(triangle.P1.x, 0, triangle.P1.y));
                    vertices.Add(new Vector3(triangle.P2.x, 0, triangle.P2.y));

                    // add triangle to mesh according to owner
                    triangles[playerIndex].Add(curCount);
                    triangles[playerIndex].Add(curCount + 1);
                    triangles[playerIndex].Add(curCount + 2);
                }
            }

            // update mesh
            m_meshFilter.mesh.vertices = vertices.ToArray();
            m_meshFilter.mesh.SetTriangles(triangles[0], 0);
            m_meshFilter.mesh.SetTriangles(triangles[1], 1);
            m_meshFilter.mesh.SetTriangles(triangles[2], 2);
            m_meshFilter.mesh.SetTriangles(triangles[3], 3);
            m_meshFilter.mesh.RecalculateBounds();

            // set correct uv
            var newUVs = new List<Vector2>();
            foreach (var vertex in vertices)
            {
                newUVs.Add(new Vector2(vertex.x, vertex.z));
            }
            m_meshFilter.mesh.uv = newUVs.ToArray();
        }

    }
}