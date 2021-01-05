namespace Shepherd
{
    using General.Controller;
    using General.Menu;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
    using Util.Algorithms.DCEL;
    using Util.Geometry;
    using Util.Geometry.DCEL;
    using Util.Geometry.Duality;
    using Util.Geometry.Polygon;
    using Util.Geometry.Triangulation;
    using Util.Algorithms.Triangulation;

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

        [SerializeField]
        private GameObject selection;
        [SerializeField]
        private GameObject continueButton;

        public Text text;

        private List<GameObject> m_sheep = new List<GameObject>();
        private List<GameObject> m_shepherds = new List<GameObject>();

        private bool m_levelSolved;
        private bool m_restartLevel;

        private static List<Color> Colors = new List<Color>(){Color.red, new Color(0f, 0.6f, 0f), Color.yellow, new Color(0f, 0.4f, 1f)};

        private Dictionary<Vector2, int> shepherdLocs = new Dictionary<Vector2, int>();
        private int budget;

        private List<Vector3> buttonLocs;

        public GameObject shepherd;
        
        private bool shepherdColour1 = true;
        private bool shepherdColour2;
        private bool shepherdColour3;
        private bool shepherdColour4;

        // mapping of vertices to ownership enum
        private readonly Dictionary<Vector2, int> m_ownership = new Dictionary<Vector2, int>();

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
            buttonLocs = new List<Vector3>() {
                GameObject.Find("RedShep").transform.position,
                GameObject.Find("GrnShep").transform.position,
                GameObject.Find("YelShep").transform.position,
                GameObject.Find("BluShep").transform.position
            };

            VoronoiDrawer.CreateLineMaterial();
        }

        void Update()
        {
            if (Input.GetKeyDown("c"))
            {
                VoronoiDrawer.CircleOn = !VoronoiDrawer.CircleOn;
            }

            if (Input.GetKeyDown("e"))
            {
                VoronoiDrawer.EdgesOn = !VoronoiDrawer.EdgesOn;
            }

            if (Input.GetKeyDown("v"))
            {
                VoronoiDrawer.VoronoiOn = !VoronoiDrawer.VoronoiOn;
            }

            // Handle mouse clicks
            if (Input.GetMouseButtonDown(0)) {
                // Cast a ray, get everything it hits
                RaycastHit2D[] hit = Physics2D.RaycastAll(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero, Mathf.Infinity);

                if (hit.Length > 0)
                {
                    // Grab the top hit GameObject
                    GameObject lastHitObject = hit[hit.Length - 1].collider.gameObject;
                    if (lastHitObject.name == "shepherd(Clone)")
                    {
                        m_shepherds.Remove(lastHitObject);
                        shepherdLocs.Remove(lastHitObject.transform.position);

                        m_delaunay = Delaunay.Create();
                        foreach (KeyValuePair<Vector2, int> o in shepherdLocs)
                        {
                            Delaunay.AddVertex(m_delaunay, o.Key);
                            m_delaunay.SetOwner(o.Key, o.Value);
                        }
                        m_dcel = Voronoi.Create(m_delaunay);
                        VoronoiDrawer.setDCEL(m_dcel);
                        // Create vertical decomposition and check solution
                        if (shepherdLocs.Count > 0)
                        {
                            VerticalDecomposition vd = VertDecomp(m_dcel);
                            Debug.LogAssertion("The current solution is " + (CheckSolution(vd) ? "correct!" : "wrong!"));

                            VoronoiDrawer.SetVD(vd);
                        } else
                        {
                            // Bit janky, assumes no shepherds => always false (so each level must have a sheep)
                            // But otherwise empty solutions will be seen as correct
                            Debug.LogAssertion("The current solution is wrong!");
                            continueButton.SetActive(false);
                        }

                        UpdateMesh();

                        Destroy(lastHitObject);
                    }
                }
                else {
                    var mousePos = Input.mousePosition;
                    if (!EventSystem.current.IsPointerOverGameObject() && shepherdLocs.Count < budget) {
                        mousePos.z = 2.0f;
                        var objectPos = Camera.main.ScreenToWorldPoint(mousePos);
                        var obj = Instantiate(shepherd, objectPos, Quaternion.identity);
                        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                        sr.color = Colors[m_activeShepherd];

                        // The new vertex
                        var me = new Vector2(objectPos.x, objectPos.y);

                        // store owner of vertex
                        shepherdLocs.Add(me, m_activeShepherd);
                        m_shepherds.Add(obj);

                        //Add vertex to the triangulation and update the voronoi
                        Delaunay.AddVertex(m_delaunay, me);
                        m_delaunay.SetOwner(me, m_activeShepherd);//shepherdColour1 ? EOwnership.PLAYER1 :
                                                                  //shepherdColour2 ? EOwnership.PLAYER2 :  shepherdColour3 ? EOwnership.PLAYER3 : EOwnership.PLAYER4)
                        m_dcel = Voronoi.Create(m_delaunay);
                        VoronoiDrawer.setDCEL(m_dcel);
                        // Create vertical decomposition and check solution
                        VerticalDecomposition vd = VertDecomp(m_dcel);
                        CheckSolution(vd);

                        VoronoiDrawer.SetVD(vd);
                        UpdateMesh();
                        
                    }
                }
                text.text = "Shepherds: " + shepherdLocs.Count + "/" + budget;
            }
        }

        private void OnRenderObject()
        {
            GL.PushMatrix();

            // Set transformation matrix for drawing to
            // match our transform
            GL.MultMatrix(transform.localToWorldMatrix);

            VoronoiDrawer.Draw(m_delaunay);

            GL.PopMatrix();
        }

        public void SetActiveShepherd(int owner) {
            m_activeShepherd = owner;
            selection.transform.position = buttonLocs[owner];
        }

        // Anne
        public void InitLevel()
        {
            foreach (var sheep in m_sheep) Destroy(sheep);
            foreach (var shepherd in m_shepherds) Destroy(shepherd);

            m_sheep.Clear();

            m_levelSolved = false;
            m_restartLevel = false;
            m_activeShepherd = 0;
            continueButton.SetActive(false);
            var level = m_levels[m_levelCounter];
            budget = level.ShepherdBudget;

            shepherdLocs = new Dictionary<Vector2, int>();

            for (int i = 0; i < level.SheepList.Count; i++)
            {
                var sheep = level.SheepList[i];
                var type = level.SheepTypes[i];
                var pos = new Vector3(sheep.x, sheep.y, -1);
                var obj = Instantiate(m_sheepPrefab, pos, Quaternion.identity) as GameObject;
                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                OwnerScript os = obj.GetComponent<OwnerScript>();
                sr.color = Colors[type];
                os.SetOwner(type);
                m_sheep.Add(obj);
            }
            
            m_delaunay = Delaunay.Create();
            m_dcel = Voronoi.Create(m_delaunay);
            VoronoiDrawer.setDCEL(m_dcel);
            UpdateMesh();
            text.text = "Shepherds: " + shepherdLocs.Count + "/" + budget;
            StartVoronoi();
        }

        /*
         * Check if a solution is correct by checking if every sheep is within the correct area
         */
         // Yuri
        public bool CheckSolution(VerticalDecomposition vd)
        {
            int wrong = 0;
            foreach (GameObject sheep in this.m_sheep)
            {
                Vector2 sheep_pos = new Vector2(sheep.transform.position.x, sheep.transform.position.y);
                // Check if the owner of the area that the sheep is located in is equal to the sheeps owner
                Trapezoid trap = vd.Search(sheep_pos);
                Face area = trap.bottom.face;
                Debug.Log("Face corresponding to the area of the sheep position: " + area + "\nArea owner: " + area.owner + "\n" + trap.show());
                if (area.owner != sheep.GetComponent<OwnerScript>().GetOwner())
                {
                    wrong += 1;
                }
            }
            Debug.LogAssertion("The current solution is " + (wrong == 0 ? "correct!" : "wrong!") + "\n"
                + (this.m_sheep.Count - wrong) + " out of " + this.m_sheep.Count + " correct");

            bool solved = wrong == 0;
            continueButton.SetActive(solved);
            return solved;
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
                m_ownership.Add(vertex, 0);
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
            VoronoiDrawer.setDCEL(dcel);
            return dcel;
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