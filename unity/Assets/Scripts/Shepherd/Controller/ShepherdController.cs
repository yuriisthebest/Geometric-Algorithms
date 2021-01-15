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
        private List<ShepherdLevel> m_levels; // List of manually created levels

        [Header("Sheep Prefabs")]
        [SerializeField]
        private GameObject m_sheepPrefab;
        [SerializeField]
        private GameObject m_shepherdPrefab; // Shepherd prefab
        [SerializeField]
        private MeshFilter m_meshFilter; // Meshfilter for colored Voronoi

        [SerializeField]
        private int m_levelCounter = 0; // Current level number
        [SerializeField]
        private int m_activeShepherd = 0; // Selected shepherd for placement

        [SerializeField]
        private GameObject selection; // Selection frame for shepherd button
        [SerializeField]
        private GameObject continueButton;

        // parameter to see if we are in the endless random level mode
        [SerializeField]
        protected bool m_endlessMode = false;

        public Text shepherdCount_text;
        public Text levelCount_text;

        private List<GameObject> m_sheep = new List<GameObject>(); // List of Sheep clones
        private List<GameObject> m_shepherds = new List<GameObject>(); // List of Shepherd clones

        // Sheep colors
        private static List<Color> Colors = new List<Color>() { Color.red, new Color(0f, 0.85f, 0f), Color.yellow, new Color(0f, 0.4f, 1f) };

        // List all shepherd locations
        private Dictionary<Vector2, int> shepherdLocs = new Dictionary<Vector2, int>();
        // Shepherd budget for current level
        private int budget;

        // Locations of shepherd buttons, for moving the selection frame
        private List<Vector3> buttonLocs;

        // Voronoi objects
        private Triangulation m_delaunay;
        private Polygon2D m_meshRect;

        private DCEL m_dcel;  //Voronoi DCEL

        // Start of the game
        void Start()
        {
            m_levelCounter = 0;
            levelCount_text.text = (m_levelCounter + 1).ToString();

            // Save button locations for selection frame
            buttonLocs = new List<Vector3>() {
                GameObject.Find("RedShep").transform.position,
                GameObject.Find("GrnShep").transform.position,
                GameObject.Find("YelShep").transform.position,
                GameObject.Find("BluShep").transform.position
            };


            InitLevel(); // Start level

            VoronoiDrawer.CreateLineMaterial(); // Create material for drawing Voronoi & VD lines
        }

        // Handle inputs each frame
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
            if (Input.GetMouseButtonUp(0)) // LMB was clicked this frame
            {
                // Cast a ray, get everything it hits
                RaycastHit2D[] hit = Physics2D.RaycastAll(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero, Mathf.Infinity);

                if (hit.Length > 0) // If we hit something
                {
                    // Grab the top hit GameObject
                    GameObject lastHitObject = hit[hit.Length - 1].collider.gameObject;

                    // If a shepherd was clicked, remove it
                    if (lastHitObject.name == "shepherd(Clone)")
                    {
                        m_shepherds.Remove(lastHitObject); // remove from shepherd clone list
                        shepherdLocs.Remove(lastHitObject.transform.position); // remove location from list
                        Destroy(lastHitObject);
                        // Create new Delaunay triangulation
                        m_delaunay = Delaunay.Create();
                        foreach (KeyValuePair<Vector2, int> o in shepherdLocs)
                        {
                            Delaunay.AddVertex(m_delaunay, o.Key);
                            m_delaunay.SetOwner(o.Key, o.Value);
                        }

                        // Create new Voronoi diagram
                        m_dcel = Voronoi.Create(m_delaunay);
                        VoronoiDrawer.setDCEL(m_dcel);

                        // Create vertical decomposition and check solution
                        if (shepherdLocs.Count > 0)
                        {
                            VerticalDecomposition vd = VertDecomp(m_dcel);
                            Debug.LogAssertion("The current solution is " + (CheckSolution(vd) == 0 ? "correct!" : "wrong!"));

                            VoronoiDrawer.SetVD(vd);
                        }
                        else
                        {
                            // If we do not have any shepherds, do not create vertical decomposition
                            // Solution without shepherds is always wrong
                            Debug.LogAssertion("The current solution is wrong!");
                            continueButton.SetActive(false);
                            UpdateText(m_sheep.Count);
                        }

                        // Update Voronoi drawing
                        UpdateMesh();


                    }
                }
                else // LMB was clicked on empty space
                {
                    // Add shepherd at mouse location
                    var mousePos = Input.mousePosition;

                    if (!EventSystem.current.IsPointerOverGameObject() && shepherdLocs.Count < budget)
                    {
                        mousePos.z = 2.0f;
                        var objectPos = Camera.main.ScreenToWorldPoint(mousePos);
                        var obj = Instantiate(m_shepherdPrefab, objectPos, Quaternion.identity);
                        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                        sr.color = Colors[m_activeShepherd];

                        // The new vertex
                        var me = new Vector2(objectPos.x, objectPos.y);

                        // store owner of vertex
                        shepherdLocs.Add(me, m_activeShepherd);
                        m_shepherds.Add(obj);

                        // Add vertex to the triangulation
                        Delaunay.AddVertex(m_delaunay, me);
                        m_delaunay.SetOwner(me, m_activeShepherd);

                        // Update Voronoi
                        m_dcel = Voronoi.Create(m_delaunay);
                        VoronoiDrawer.setDCEL(m_dcel);

                        // Create vertical decomposition and check solution
                        VerticalDecomposition vd = VertDecomp(m_dcel);
                        CheckSolution(vd);

                        // Update VD in drawer
                        VoronoiDrawer.SetVD(vd);

                        // Update Voronoi drawing
                        UpdateMesh();

                    }
                }
            }
        }

        /// <summary>
        /// Once scene has been rendered, overlay delaunay/voronoi/vertical decomposition using OpenGL
        /// </summary>
        private void OnRenderObject()
        {
            GL.PushMatrix();

            // Set transformation matrix for drawing to
            // match our transform
            GL.MultMatrix(transform.localToWorldMatrix);

            VoronoiDrawer.Draw(m_delaunay);

            GL.PopMatrix();
        }

        /// <summary>
        /// Update active shepherd and move selection frame in UI
        /// </summary>
        /// <param name="owner">New active shepherd id</param>

        /// <summary>
        /// Initialize the current level (based on m_levelCounter)
        /// </summary>
        public void InitLevel()
        {
            // clear the previous level and set all variables to their
            // original value
            foreach (var sheep in m_sheep) Destroy(sheep);
            foreach (var shepherd in m_shepherds) Destroy(shepherd);

            m_sheep.Clear();
            SetActiveShepherd(0);
            continueButton.SetActive(false);
            shepherdLocs = new Dictionary<Vector2, int>();

            // Load level
            ShepherdLevel level;
            if (m_endlessMode)
            {
                // Create randomly generated level
                level = CreateEndlessLevel(m_levelCounter);
            }
            else
            {
                // Use premade level
                level = m_levels[m_levelCounter];
            }

            // Update shepherd budget
            budget = level.ShepherdBudget;

            // Add all sheep from level
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

            // Update Voronoi
            StartVoronoi();
            UpdateMesh();
            UpdateText(m_sheep.Count);

        }

        /// <summary>
        /// Restart the current random level
        /// </summary>
        public void ResetLevel()
        {
            // Remove all shepherds while keeping sheep
            foreach (var shepherd in m_shepherds) Destroy(shepherd);
            m_activeShepherd = 0;
            continueButton.SetActive(false);
            shepherdLocs = new Dictionary<Vector2, int>();
            StartVoronoi();
            UpdateMesh();
            UpdateText(m_sheep.Count);
        }

        // Advance the level or give the victory screen
        public void AdvanceLevel()
        {
            m_levelCounter++;
            levelCount_text.text = (m_levelCounter + 1).ToString();

            if (m_levelCounter >= m_levels.Count & !m_endlessMode) // Normal mode finished
            {
                SceneManager.LoadScene("shepherdVictory"); // Display victory screen
            }
            else
            {
                InitLevel(); // Initialize next level
            }

        }

        // Random endless level generation
        // Determines the amount of shepherds placed based on the current level number
        private ShepherdLevel CreateEndlessLevel(int level)
        {
            // create the output scriptable object
            var asset = ScriptableObject.CreateInstance<ShepherdLevel>();

            // place the shepherds and sheep randomly
            List<Vector2> shepherds = RandomPos(level + 4);
            List<Vector2> sheep = RandomPos(2 * (level + 4));

            // Print locations
            string sls = "Shepherd locations: \n";
            foreach (Vector2 v in shepherds)
            {
                sls += "(" + v.x + ", " + v.y + "), ";
            }
            Debug.Log(sls);
            string shls = "Sheep locations: \n";
            foreach (Vector2 v in sheep)
            {
                shls += "(" + v.x + ", " + v.y + "), ";
            }
            Debug.Log(shls);

            // Construct the voronoi diagram corresponding to the shepherd locations
            StartVoronoi();
            foreach (Vector2 me in shepherds)
            {
                // Add vertex to the triangulation and update the voronoi
                Delaunay.AddVertex(m_delaunay, me);
                m_delaunay.SetOwner(me, Random.Range(0, 4));
                m_dcel = Voronoi.Create(m_delaunay);
            }

            // Create vertical decomposition
            VerticalDecomposition vd = VertDecomp(m_dcel);

            // Use the vertical decomposition to determine the ownership of each sheep
            // and add the sheep to the level
            foreach (Vector2 s in sheep)
            {
                Trapezoid trap = vd.Search(s);
                Face area = trap.bottom.face;
                int i = area.owner;
                asset.addSheep(s, i);
            }

            // Normalize coordinates
            var rect = BoundingBoxComputer.FromPoints(asset.SheepList);
            asset.SheepList = Normalize(rect, 6f, asset.SheepList);

            // Set shepherd budget
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
        /// Returns count distinct random positions not on the boundary
        /// </summary>
        /// <param name="count"> The number of positions returned</param>
        /// <returns></returns>
        protected List<Vector2> RandomPos(int count)
        {
            var result = new List<Vector2>();

            while (result.Count < count)
            {
                // find uniform random positions in the rectangle (0,0) (1,1)
                var xpos = Random.Range(0.0f, 6.0f);
                var ypos = Random.Range(0.0f, 6.0f);
                var pos = new Vector2(xpos, ypos);
                result.Add(pos);

            }

            return result;
        }

        public void SetActiveShepherd(int owner)
        {
            m_activeShepherd = owner;
            selection.transform.position = buttonLocs[owner];
        }

        /// <summary>
        /// Create vertical decomposition based on InGraph DCEL
        /// </summary>
        /// <param name="InGraph">The DCEL to use</param>
        /// <returns>Vertical decomposition</returns>
        public VerticalDecomposition VertDecomp(DCEL InGraph)
        {
            return new VerticalDecomposition(InGraph, m_meshFilter);
        }

        /// <summary>
        /// Check if the current solution is correct by checking if every sheep is within the correct area
        /// </summary>
        /// <param name="vd">The vertical decomposition</param>
        /// <returns>Number of wrong sheep</returns>
        public int CheckSolution(VerticalDecomposition vd)
        {
            int wrong = 0;
            foreach (GameObject sheep in this.m_sheep)
            {
                Vector2 sheep_pos = new Vector2(sheep.transform.position.x, sheep.transform.position.y);
                // Check if the owner of the area that the sheep is located in is equal to the sheeps owner
                Trapezoid trap = vd.Search(sheep_pos);
                Face area = trap.bottom.face;
                // Debug.Log("Face corresponding to the area of the sheep position: " + area + "\nArea owner: " + area.owner + "\n" + trap.show());
                if (area.owner != sheep.GetComponent<OwnerScript>().GetOwner())
                {
                    wrong += 1;
                }
            }

            Debug.LogAssertion("The current solution is " + (wrong == 0 ? "correct!" : "wrong!") + "\n"
                + (this.m_sheep.Count - wrong) + " out of " + this.m_sheep.Count + " correct");

            // Update shepherd count text
            UpdateText(wrong);

            continueButton.SetActive(wrong == 0);
            return wrong;
        }

        // Dummy CheckSolution for no parameters
        public void CheckSolution() { }

        // Initial call for the construction of the Voronoi diagram
        public void StartVoronoi()
        {
            m_delaunay = Delaunay.Create();
            m_dcel = Voronoi.Create(m_delaunay);
            VoronoiDrawer.setDCEL(m_dcel);

        }

        /// <summary>
        /// Updates the mesh according to the Voronoi DCEL.
        /// (Edited from existing Voronoi game)
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
                var owner = face.owner;

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
                    triangles[owner].Add(curCount);
                    triangles[owner].Add(curCount + 1);
                    triangles[owner].Add(curCount + 2);
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

        private void UpdateText(int wrong)
        {
            shepherdCount_text.text = "Shepherds: " + shepherdLocs.Count + "/" + budget + "\nIncorrect sheep: " + wrong;
        }

    }
}