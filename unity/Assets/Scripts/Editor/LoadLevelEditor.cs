
using Shepherd;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using Util.Geometry;


[ScriptedImporter(1, "ipe")]
public class LoadLevelEditor : ScriptedImporter
{
    private readonly float shSIZE = 6f;

    /// <summary>
    /// Defines a custom method for importing .ipe files into unity.
    /// Currently used for importing levels into 
    /// </summary>
    /// <param name="ctx"></param>
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var path = ctx.assetPath;
        var name = Path.GetFileNameWithoutExtension(path);

        var fileSelected = XElement.Load(path);

        // switch between which level to generate based on file name
        UnityEngine.Object obj;
        if (name.StartsWith("shepherdLevel"))
        {
            obj = LoadShepherdLevel(fileSelected, name);
        }
        else
        {
            // no file name match
            EditorUtility.DisplayDialog("Error", "Level name not in an expected format", "OK");
            ctx.SetMainObject(null);
            return;
        }

        // add generated level as the main imported file
        ctx.AddObjectToAsset(name, obj);
        ctx.SetMainObject(obj);
    }

    private UnityEngine.Object LoadShepherdLevel(XElement fileSelected, string name)
    {
        // create the output scriptable object
        var asset = ScriptableObject.CreateInstance<ShepherdLevel>();

        // retrieve page data from .ipe file
        var items = fileSelected.Descendants("page").First().Descendants("use");

        // get marker data into respective vector list
        List<string> markerTypes = new List<string> { "disk", "square", "cross", "circle" };

        for (int i = 0; i < markerTypes.Count; i++)
        {
            List<Vector2> sheepLocs = GetMarkers(items, markerTypes[i]);
            foreach (Vector2 l in sheepLocs)
            {
                asset.addSheep(l, i);
            }

        }

        // normalize coordinates

        var rect = BoundingBoxComputer.FromPoints(asset.SheepList);
        asset.SheepList = Normalize(rect, shSIZE, asset.SheepList);


        // give warning if no relevant data found
        if (asset.SheepList.Count == 0)
        {
            EditorUtility.DisplayDialog("Warning", "File does not contain any valid markers.", "OK");
        }

        asset.setBudget(int.Parse(name.Split('_').Last()));

        return asset;
    }

    /// <summary>
    /// Retrieve a vector list for all markers elements with given name
    /// </summary>
    /// <param name="items"></param>
    /// <param name="markerName"></param>
    /// <returns>list of positions</returns>
    private List<Vector2> GetMarkers(IEnumerable<XElement> items, string markerName)
    {
        var result = new List<Vector2>();
        var markers = items.Where(x => x.Attribute("name").Value.Contains(markerName));

        foreach (var marker in markers)
        {
            // retrieve (x, y) position from pos attribute
            var x = float.Parse(marker.Attribute("pos").Value.Split(' ')[0]);
            var y = float.Parse(marker.Attribute("pos").Value.Split(' ')[1]);

            if (marker.Attribute("matrix") != null)
            {
                var transformation = marker.Attribute("matrix").Value
                    .Split(' ')
                    .Select(s => float.Parse(s))
                    .ToList();

                // apply transformation matrix (could be made into library function)
                x = transformation[0] * x + transformation[2] * y + transformation[4];
                y = transformation[1] * x + transformation[3] * y + transformation[5];
            }

            // add to result
            result.Add(new Vector2(x, y));
        }

        return result;
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
                (p[1] - (rect.yMin + rect.height / 2f)) * scale))
            .ToList();
    }

}