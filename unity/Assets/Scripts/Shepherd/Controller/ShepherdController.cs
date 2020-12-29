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

    // Shepherd ownership
    public enum EOwnership
    {
        UNOWNED,
        PLAYER1,
        PLAYER2,
        PLAYER3,
        PLAYER4
    }

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

        [SerializeField]
        private int m_levelCounter = 0;
        [SerializeField]
        private int m_activeShepherd = 0;

        private List<GameObject> m_sheep = new List<GameObject>();

        private bool m_levelSolved;
        private bool m_restartLevel;

        private static List<Color> Colors = new List<Color> {Color.red, Color.green, Color.yellow, Color.blue};

        public GameObject shepherd;
        private bool cooldown = false;

        
        private bool shepherdColour1 = true;
        private bool shepherdColour2;
        private bool shepherdColour3;
        private bool shepherdColour4;

        // mapping of vertices to ownership enum
        private readonly Dictionary<Vector2, EOwnership> m_ownership = new Dictionary<Vector2, EOwnership>();

        // Voronoi objects
        private Triangulation m_delaunay;
        private Polygon2D m_meshRect;

        [SerializeField]
        private int playerIndex;
        
        private DCEL m_dcel;

        private System.Random rd = new System.Random();

        void Start()
        {
            InitLevel();
            StartVoronoi();
        }

        void Update()
        {
            // Add a shepherd to the game when the user clicks
            if (Input.GetMouseButton(0) && !cooldown) {
                var mousePos = Input.mousePosition;
                if (mousePos.y > 75) {
                    mousePos.z = 2.0f;
                    var objectPos = Camera.main.ScreenToWorldPoint(mousePos);
                    var obj = Instantiate(shepherd, objectPos, Quaternion.identity);
                    SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                    sr.color = Colors[m_activeShepherd];
                    // Don't let the user spam a lot of shepherds
                    Invoke("ResetCooldown", 0.5f);
                    cooldown = true;

                    // The new vertex
                    var me = new Vector2(objectPos.x, objectPos.y);

                    // store owner of vertex
                    m_ownership.Add(me, shepherdColour1 ? EOwnership.PLAYER1 :
                        shepherdColour2 ? EOwnership.PLAYER2 :  shepherdColour3 ? EOwnership.PLAYER3 : EOwnership.PLAYER4);


                    //Add vertex to the triangulation and update the voronoi
                    Delaunay.AddVertex(m_delaunay, me);
                    m_delaunay.SetOwner(me, m_activeShepherd);//shepherdColour1 ? EOwnership.PLAYER1 :
                        //shepherdColour2 ? EOwnership.PLAYER2 :  shepherdColour3 ? EOwnership.PLAYER3 : EOwnership.PLAYER4)
                
                    m_dcel = Voronoi.Create(m_delaunay);

                    UpdateMesh();

                    // Test: draw a square of random color next to placed sheep
                    //m_dcel = new DCEL();
                    //var v1 = m_dcel.AddVertex(new Vector2(objectPos.x, objectPos.y));
                    //var v2 = m_dcel.AddVertex(new Vector2(objectPos.x + 1, objectPos.y));
                    //var v3 = m_dcel.AddVertex(new Vector2(objectPos.x + 1, objectPos.y + 1));
                    //var v4 = m_dcel.AddVertex(new Vector2(objectPos.x, objectPos.y + 1));

                    //m_dcel.AddEdge(v1, v2);
                    //m_dcel.AddEdge(v2, v3);
                    //m_dcel.AddEdge(v3, v4);
                    // HalfEdge e1 = m_dcel.AddEdge(v4, v1);

                    //e1.Twin.Face.owner = rd.Next(4);

                }
                
            }
        }

        private void ResetCooldown() {
            cooldown = false;
        }

        public void SetActiveShepherd(int owner) {
            m_activeShepherd = owner;
        }

        // Anne
        public void InitLevel()
        {
            foreach (var sheep in m_sheep) Destroy(sheep);

            m_sheep.Clear();

            m_levelSolved = false;
            m_restartLevel = false;
            m_activeShepherd = 1;

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

        /*
         * Check if a solution is correct by checking if every sheep is within the correct area
         */
         // Yuri
        public bool CheckSolution(VerticalDecomposition vd)
        {
            foreach (GameObject sheep in this.m_sheep)
            {
                Vector2 sheep_pos = new Vector2(sheep.transform.position.x, sheep.transform.position.y);
                // Check if the owner of the area that the sheep is located in is equal to the sheeps owner
                // TODO, find color / owner of sheep and link it to the owner
                //sheep.GetComponent<SpriteRenderer>().color;
                if (vd.Search(sheep_pos).bottom.face.owner != 1)
                {
                    return false;
                }
            }
            return true;
        }
        // I get errors if I don't implement this function (since I added parameter in function above)
        public void CheckSolution() { }

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
            return new VerticalDecomposition(InGraph, m_meshFilter);
        }

        // Christine
        public void StartVoronoi()
        { 
            // create initial delaunay triangulation (three far-away points)
            // TODO: make sure that the sheep are not taken into account!!
            m_delaunay = Delaunay.Create();

            // add auxiliary vertices as unowned
            foreach (var vertex in m_delaunay.Vertices)
            {
                m_ownership.Add(vertex, EOwnership.UNOWNED);
            }

            // create polygon of rectangle window for intersection with voronoi
            float z = Vector2.Distance(m_meshFilter.transform.position, Camera.main.transform.position);
            var bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, z));
            var topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, z));
            m_meshRect = new Polygon2D(
                new List<Vector2>() {
                    new Vector2(bottomLeft.x, bottomLeft.z),
                    new Vector2(bottomLeft.x, topRight.z),
                    new Vector2(topRight.x, topRight.z),
                    new Vector2(topRight.x, bottomLeft.z)
                });

            VoronoiDrawer.CreateLineMaterial();
        }

        // Christine
        public static DCEL CreateVoronoi(Triangulation m_delaunay)
        {
            if (!Delaunay.IsValid(m_delaunay))
            {
                throw new GeomException("Triangulation should be delaunay for the Voronoi diagram.");
            }

            var dcel = new DCEL();

            // create vertices for each triangles circumcenter and store them in a dictionary
            Dictionary<Triangle, DCELVertex> vertexMap = new Dictionary<Triangle, DCELVertex>();
            foreach (var triangle in m_delaunay.Triangles)
            {
                // degenerate triangle, just ignore
                if (!triangle.Circumcenter.HasValue) continue;

                var vertex = new DCELVertex(triangle.Circumcenter.Value);
                dcel.AddVertex(vertex);
                vertexMap.Add(triangle, vertex);
            }

            // remember which edges where visited
            // since each edge has a twin
            var edgesVisited = new HashSet<TriangleEdge>();

            foreach (var edge in m_delaunay.Edges)
            {
                // either already visited twin edge or edge is outer triangle
                if (edgesVisited.Contains(edge) || edge.IsOuter) continue;

                // add edge between the two adjacent triangles vertices
                // vertices at circumcenter of triangle
                if (edge.T != null && edge.Twin.T != null)
                {
                    var v1 = vertexMap[edge.T];
                    var v2 = vertexMap[edge.Twin.T];

                    dcel.AddEdge(v1, v2);

                    edgesVisited.Add(edge);
                    edgesVisited.Add(edge.Twin);
                }
            }

            return dcel;
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