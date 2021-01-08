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

        // parameter to see if we are in the endless random level mode
        [SerializeField]
        protected bool m_endlessMode = false;

        public Text text;
        public Text levelCount_text;

        private List<GameObject> m_sheep = new List<GameObject>();
        private List<GameObject> m_shepherds = new List<GameObject>();

        private static List<Color> Colors = new List<Color>(){Color.red, new Color(0f, 0.85f, 0f), Color.yellow, new Color(0f, 0.4f, 1f)};

        private Dictionary<Vector2, int> shepherdLocs = new Dictionary<Vector2, int>();
        private int budget;

        private List<Vector3> buttonLocs;

        public GameObject shepherd;

        // Voronoi objects
        private Triangulation m_delaunay;
        private Polygon2D m_meshRect;

        [SerializeField]
        private int playerIndex;
        
        private DCEL m_dcel;

        // start of a level
        void Start()
        {
            m_levelCounter = 0;
            levelCount_text.text = (m_levelCounter + 1).ToString();
            buttonLocs = new List<Vector3>() {
                GameObject.Find("RedShep").transform.position,
                GameObject.Find("GrnShep").transform.position,
                GameObject.Find("YelShep").transform.position,
                GameObject.Find("BluShep").transform.position
            };
            InitLevel();
            VoronoiDrawer.CreateLineMaterial();
        }

        // update the level given an input
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
                            Debug.LogAssertion("The current solution is " + (CheckSolution(vd) == 0 ? "correct!" : "wrong!"));

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
                        m_delaunay.SetOwner(me, m_activeShepherd);
                        m_dcel = Voronoi.Create(m_delaunay);
                        VoronoiDrawer.setDCEL(m_dcel);
                        // Create vertical decomposition and check solution
                        VerticalDecomposition vd = VertDecomp(m_dcel);
                        CheckSolution(vd);

                        VoronoiDrawer.SetVD(vd);
                        UpdateMesh();
                        
                    }
                }
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

        // Anne & Christine 
        // initialize level
        public void InitLevel()
        {
            // clear the previous level and set all variables to their
            // original value
            foreach (var sheep in m_sheep) Destroy(sheep);
            foreach (var shepherd in m_shepherds) Destroy(shepherd);

            m_sheep.Clear();

            SetActiveShepherd(0);
            continueButton.SetActive(false);
            ShepherdLevel level;

            if (m_endlessMode)
            {
                level = InitEndlessLevel(m_levelCounter);
            }
            else
            {
                level = m_levels[m_levelCounter];
            }

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

            StartVoronoi();
            UpdateMesh();
            text.text = "Shepherds: " + shepherdLocs.Count + "/" + budget + "\nIncorrect sheep: " + m_sheep.Count;

        }

        // reset random level
        public void ResetRandomLevel()
        {
            foreach (var shepherd in m_shepherds) Destroy(shepherd);
            m_activeShepherd = 0;
            continueButton.SetActive(false);
            shepherdLocs = new Dictionary<Vector2, int>();
            StartVoronoi();
            UpdateMesh();
            text.text = "Shepherds: " + shepherdLocs.Count + "/" + budget + "\nIncorrect sheep: " + m_sheep.Count;
        }

        // advance the level or give the victory screen
        public void AdvanceLevel()
        {
            m_levelCounter++;
            levelCount_text.text = (m_levelCounter + 1).ToString();
            if (m_levelCounter == m_levels.Count & !m_endlessMode)
            {
                SceneManager.LoadScene("shepherdVictory");
            }
            else
            {
                InitLevel();
            }

        }


        // Random endless level generation
        // Determines the amount of shepherds placed based on the current level
        private ShepherdLevel InitEndlessLevel(int level)
        {
           // create the output scriptable object
            var asset = ScriptableObject.CreateInstance<ShepherdLevel>();

            // place the shepherds and sheep randomly
            List<Vector2> shepherds = RandomPos(level + 4);
            List<Vector2> sheep = RandomPos(2*(level + 4));

            // construct the voronoi diagram corresponding to the shepherds
            // locations
            StartVoronoi();
            foreach (Vector2 me in shepherds)
            {
                m_activeShepherd = Random.Range(0, 4);
                //Add vertex to the triangulation and update the voronoi
                Delaunay.AddVertex(m_delaunay, me);
                m_delaunay.SetOwner(me, m_activeShepherd);
                m_dcel = Voronoi.Create(m_delaunay);
            }

            // create vertical decomposition
            VerticalDecomposition vd = VertDecomp(m_dcel);

            // use the vertical decomposition to determine the ownership of each sheep
            // and add the sheep to the level

            foreach(Vector2 s in sheep)
            {
                Trapezoid trap = vd.Search(s);
                Face area = trap.bottom.face;
                int i = area.owner;
                asset.addSheep(s, i);
            }

            // normalize coordinates

            var rect = BoundingBoxComputer.FromPoints(asset.SheepList);
            asset.SheepList = Normalize(rect, 6f, asset.SheepList);

            asset.setBudget(shepherds.Count);
            return asset;
        }


        /// <summary>
        /// Normalizes the coordinate vector to fall within bounds specified by rect.
        /// Also adds random perturbations to create general positions.
        /// </summary>
        /// <param name="rect">Bounding box</param>
        /// <param name="coords"></param>
        private List<Vector2> Normalize(Rect rect, float SIZE, List<Vector2> coords)
        {
            var scale = SIZE / Mathf.Max(rect.width, rect.height);

            return coords
                .Select(p => new Vector2(
                    (p[0] - (rect.xMin + rect.width / 2f)) * scale,
                    (p[1] - (rect.yMin + rect.height / 2f)) * scale * 0.6f))
                .ToList();
        }


        /// <summary>
        /// Returns count non-overlaping random positions not on the boundary
        /// </summary>
        /// <param name="count"> The number of positions returned</param>
        /// <returns></returns>
        protected List<Vector2> RandomPos(int count)
        {
            var result = new List<Vector2>();

            while (result.Count < count)
            {
                // find uniform random positions in the rectangle (0,0) (1,1)
                var xpos = Random.Range(0.0f, 1.0f);
                var ypos = Random.Range(0.0f, 1.0f);
                var pos = new Vector2(xpos, ypos);
                result.Add(pos);

            }

            return result;
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


        // Anne
        // KAN DIT WEG? - Christine
        public Polygon2D LocatePoint(Vector2D p, DCEL InGraph)
        {
            var vc = VertDecomp(InGraph);
            return null;
        }


        /*
         * Check if a solution is correct by checking if every sheep is within the correct area
         */
        // Yuri
        public int CheckSolution(VerticalDecomposition vd)
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

            text.text = "Shepherds: " + shepherdLocs.Count + "/" + budget + "\nIncorrect sheep: " + wrong;
            bool solved = wrong == 0;
            continueButton.SetActive(solved);
            return wrong;
        }
        // I get errors if I don't implement this function (since I added parameter in function above)
        public void CheckSolution() { }


        // Christine
        // Initial call for the construction of the Voronoi diagram
        public void StartVoronoi()
        {
            m_delaunay = Delaunay.Create();
            m_dcel = Voronoi.Create(m_delaunay);
            VoronoiDrawer.setDCEL(m_dcel);

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