﻿using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(ShaderMeshGenerator))]
public class TerrainManager : MonoBehaviour
{
    [Header("QuadTree settings")]
    public int MaxDepth = 4; // The max amount of levels we can split

    [Header("Terrain settings")]
    public int TerrainSize = 1024;
    public bool RenderTerrain = true;
    public float HeightMultiplier = 10.0f;
    public int ResolutionMultiplier = 4;

    [Header("Split settings")]
    public bool UseDistanceMetrics = true;
    public float MaxResolutionAt = 50; // If a node is closer than this, it will be at full resolution
    public float MaxSplitRange = 200;
    public int breakPointEXP = 2;
    public bool UseBumpMetric = true;
    public float VarianceScaler = 0.1f;
    public bool UseErrorMetric = false;
    public float PixelError = 4;

    [Header("Terrain references")]
    public Texture2D HeightMap;
    public Material TerrainMaterial;
    public GameObject BlockPrefab;

    [Header("Mesh generator")]
    public ShaderMeshGenerator meshGenerator;

    // Private members
    private float BoerC;

    /// <summary>
    /// To link to terrain block data
    /// </summary>
    private class CachedMeshHolder : IPoolable
    {
        public GameObject gameObject;
        public MeshFilter filter;
        public MeshCollider collider;
        public MeshRenderer renderer;

        public void OnCreate() { }

        public void OnDestroy()
        {
            gameObject.SetActive(false);
        }
    }

    public QuadTreeNode<NodeData> root;

    // Meshpooling
    private List<CachedMeshHolder> activeMeshes = new List<CachedMeshHolder>(); // All currently shown meshes
    // Chunkpooling
    public HashSet<QuadTreeNode<NodeData>> activeNodes = new HashSet<QuadTreeNode<NodeData>>(); // All currently shown nodes
    private Dictionary<QuadTreeNode<NodeData>, CachedMeshHolder> nodeMeshMap = new Dictionary<QuadTreeNode<NodeData>, CachedMeshHolder>(); // All meshes

    public class NodeData
    {
        // This value is compared to distance from camera to decide if a node should be split or merged 
        // It is set once when a node is created
        public float Breakpoint = 50;
        public float Variance = 0.0f;
        public float ErrorMetric = 0;
        public Vector2 arrayPosition;
    }

    public void InitializeTree()
    {
        float nodesPerEdge = Mathf.Sqrt(Mathf.Pow(4, MaxDepth));
        float verticesPerNodeEdge = ResolutionMultiplier * 8;
        Debug.Log(
        "MaxDepth = " + MaxDepth + " and ResolutionMultiplier = " + ResolutionMultiplier
        +"\nNodes per edge = " + nodesPerEdge
        +"\nVertices per node edge = " + verticesPerNodeEdge
        +"\nMax heightmap size = " + nodesPerEdge * verticesPerNodeEdge);

        root = new QuadTreeNode<NodeData>(null, Vector3.zero, TerrainSize, 0);
        GenerateNodeData(root);
        activeNodes.Add(root);

        // Applying Boer's equation
        float A = 1.0f / (Mathf.Tan(Camera.main.fieldOfView * (Mathf.PI / 360.0f)));
        float T = (2.0f * PixelError) / Screen.height;
        BoerC = A / T;
        Debug.Log("(C) " + BoerC + " = (T) " + T + " / (A) " + A);

        // If we are using variance, build the entire tree directly
        if (UseBumpMetric)
        {
            VarianceSplit(root, 0, 0);
        }
        if(UseErrorMetric)
        {
            Vector2 startPos = new Vector2(0, TerrainSize - 1); // Vector2.one * (-TerrainSize / 2.0f)
            Debug.Log(startPos);
            float stepLength = TerrainSize / (ResolutionMultiplier * 8);
            ErrorMetricSplit(root, startPos, stepLength);
        }
    }

    // Pre-split the tree and calculate variance data
    // We first split to the leafs and set their variances, then we backtrack up to the root and set variance = max of subnode's variances
    void VarianceSplit(QuadTreeNode<NodeData> node, int x, int y)
    {
        // When we are a leaf at max depth, get variance from variance texture
        if (node.Depth == MaxDepth)
        {
            node.Data.arrayPosition = new Vector2(x, y);
            node.Data.Variance = GetComponent<VarianceCompressor>().VarianceArray[x, y];
            node.Data.Variance *= HeightMultiplier * BoerC / (ResolutionMultiplier * 8);
        }

        // Else subdivide the tree
        else
        {
            if(node.IsLeaf)
                Subdivide(node);

            // We multiply the array indexes X and Y by 2 because subdivide splits each square into 2 squares
            // Try using [x,y] coordinates on paper and split the squares at random places, it will make sense
            x *= 2;
            y *= 2;

            // Traverse down the tree
            VarianceSplit(node.SubNodes[0], x, y);
            VarianceSplit(node.SubNodes[1], (x + 1), y);
            VarianceSplit(node.SubNodes[2], x, (y + 1));
            VarianceSplit(node.SubNodes[3], (x + 1), (y + 1));

            // We reach here when the entire tree has been split and all leaf nodes have a variance
            // Now that we are traversing back up the tree, set the variance to the max of the children's variance
            node.Data.Variance = Mathf.Max(
                node.SubNodes[0].Data.Variance,
                node.SubNodes[1].Data.Variance,
                node.SubNodes[2].Data.Variance,
                node.SubNodes[3].Data.Variance);

            // We reach here when the entire tree has been split and all leaf nodes have a variance
            // Now that we are traversing back up the tree, set the variance to the median of the children
            /*
            node.Data.Variance = (
                node.SubNodes[0].Data.Variance +
                node.SubNodes[1].Data.Variance +
                node.SubNodes[2].Data.Variance +
                node.SubNodes[3].Data.Variance) / 4.0f;
            */
        }
    }


    // This method will traverse the tree and find the max error metric for each node
    void ErrorMetricSplit(QuadTreeNode<NodeData> node, Vector2 pos, float stepLength)
    {
        // Iterate over all vertices and find the max error metric
        float maxE = 0;

        // Use the full set of vertices to iterate over
        int vertCount = ResolutionMultiplier * 8;

        for (int x = 0; x < vertCount; x++)
        {
            for (int y = 0; y < vertCount; y++)
            {
                float eA = 0, eB = 0, eC = 0;
                // If last i-iteration, only check 1 edge
                // If last j-iteration, only check 1 edge
                // If both are at last iteration, we can skip

                // Calculate the short step, which should now be 1 pixel wide
                int shortStepLength = (int)(stepLength / 2.0f);

                // Find the position of the current vertex (top-left)
                int v0X = (int)(pos.x + (x * stepLength));
                int v0Y = (int)(pos.y - (y * stepLength));
                float v0Height = HeightMap.GetPixel(v0X, v0Y).r;

                // Calculate the error metrics as difference between interpolated and actual height map values

                // Only perform when we are not the top line of verts
                if (y < (vertCount - 1))
                {
                    int v1X = v0X;
                    int v1Y = v0Y - (int)stepLength;
                    float interpolatedHeightA = (v0Height + HeightMap.GetPixel(v1X, v1Y).r) / 2.0f;
                    eA = Mathf.Abs(HeightMap.GetPixel(v1X, v1Y - shortStepLength).r - interpolatedHeightA);
                }
                // Only perform when we are not a corner
                if (x < (vertCount - 1) && y < vertCount)
                {
                    int v2X = v0X + (int)stepLength;
                    int v2Y = v0Y - (int)stepLength;
                    float interpolatedHeightB = (v0Height + HeightMap.GetPixel(v2X, v2Y).r) / 2.0f;
                    eB = Mathf.Abs(HeightMap.GetPixel(v2X - shortStepLength, v2Y - shortStepLength).r - interpolatedHeightB);
                }
                // Only perform when we are not the rightmost line of verts
                if (x + 1 < vertCount)
                {
                    int v3X = v0X + (int)stepLength;
                    int v3Y = v0Y;
                    float interpolatedHeightC = (v0Height + HeightMap.GetPixel(v3X, v3Y).r) / 2.0f;
                    eC = Mathf.Abs(HeightMap.GetPixel(v3X - shortStepLength, v3Y).r - interpolatedHeightC);
                }

                // Choose the largest error metric
                float calcE = Mathf.Max(eA, eB, eC);
                if (calcE > maxE) maxE = calcE;
            }
        }

        // Applying Boer's calculation and heightmultiplier, Error * heightMultiplier * C
        node.Data.ErrorMetric = maxE * HeightMultiplier * BoerC;

        if (node.Depth < MaxDepth - 1)
        {
            // Subdivide and continue down the tree
            //if (node.IsLeaf)
                Subdivide(node);

            // The subdivision divides stepLength by 2
            stepLength /= 2.0f;
            float width = node.SubNodes[0].Size;

            // NW
            ErrorMetricSplit(node.SubNodes[0], new Vector2(pos.x, pos.y), stepLength);
            // NE
            ErrorMetricSplit(node.SubNodes[1], new Vector2(pos.x + width, pos.y), stepLength);
            // SW
            ErrorMetricSplit(node.SubNodes[2], new Vector2(pos.x, pos.y - width), stepLength);
            // SE
            ErrorMetricSplit(node.SubNodes[3], new Vector2(pos.x + width, pos.y - width), stepLength);
        }

        // We reach here when the entire tree has been split and all leaf nodes have a max error metric
        // Now that we are traversing back up the tree, set the error metric to the max of the children's error
        if(!node.IsLeaf)
            node.Data.ErrorMetric = Mathf.Max(
                node.Data.ErrorMetric,
                node.SubNodes[0].Data.ErrorMetric,
                node.SubNodes[1].Data.ErrorMetric,
                node.SubNodes[2].Data.ErrorMetric,
                node.SubNodes[3].Data.ErrorMetric);
        //}
    }

    void Start()
    {
        InitializeTree();
    }

    void Update()
    {
        UpdateLOD();
    }

    void UpdateLOD()
    {
        // Loop through all nodes, deciding if we should keep them or not
        HashSet<QuadTreeNode<NodeData>> newActiveNodes = new HashSet<QuadTreeNode<NodeData>>();
        foreach (QuadTreeNode<NodeData> node in activeNodes)
        {
            // Subdivide the node if possible
            // The reason we have a subdivide here is that we should branch the tree if possible
            // It's not about splitting and showing, it's about splitting just for the sake of creating the tree
            // After this split we might still be rendering the parent at some level
            if (node.IsLeaf && node.Depth < MaxDepth)
            {
                Subdivide(node);
            }

            if (!node.IsLeaf && CanSplit(node)) // Try split
            {
                ShowNodeSplit(node, newActiveNodes);
            }
            else // Try merge
            {
                if (node.Parent != null && !CanSplit(node.Parent)) // Try merge
                {
                    ShowNodeMerge(node.Parent, newActiveNodes);
                }
                else // Fallback to render this node if we failed to split and merge
                {
                    ShowNode(node, newActiveNodes);
                }
            }
        }

        //Set The Active Chunks
        activeNodes = newActiveNodes;
    }

    /// <summary>
    /// Merges 4 nodes into their parent
    /// </summary>
    private void ShowNodeMerge(QuadTreeNode<NodeData> node, HashSet<QuadTreeNode<NodeData>> newActiveList)
    {
        if (RenderTerrain)
        {
            // If there is no mesh corresponding to this node
            if (!nodeMeshMap.ContainsKey(node))
            {
                // Create an empty mesh holder object
                CachedMeshHolder container = CreateNewBlock(node);

                // Set chunk data...?
                // ...

                // Call highest detail action...?
                // ...

                // Add the newly created mesh to the lists
                activeMeshes.Add(container);

                nodeMeshMap[node] = container;
            }

            // Show the node mesh
            nodeMeshMap[node].gameObject.SetActive(true);
        }

        // Discard children nodes
        foreach (var subNode in node.SubNodes)
            DiscardNode(subNode);

        // If the newActiveNodes don't already contain this node, add it
        if (!newActiveList.Contains(node))
            newActiveList.Add(node);
    }

    /// <summary>
    /// Basically ShowNode(...) but it runs 4 times, once for each child of the passed node
    /// </summary>
    private void ShowNodeSplit(QuadTreeNode<NodeData> node, HashSet<QuadTreeNode<NodeData>> newActiveNodes)
    {
        for (int i = 0; i < 4; i++)
        {
            QuadTreeNode<NodeData> subNode = node.SubNodes[i];

            if (RenderTerrain)
            {
                // If there is no mesh corresponding to this node
                if (RenderTerrain && !nodeMeshMap.ContainsKey(subNode))
                {
                    // Create an empty mesh holder object
                    CachedMeshHolder container = CreateNewBlock(subNode);

                    // Set chunk data...?
                    // ...

                    // Call highest detail action...?
                    // ...

                    // Add the newly created mesh to the lists
                    activeMeshes.Add(container);
                    nodeMeshMap[subNode] = container;
                }

                // Show the subNode mesh
                nodeMeshMap[subNode].gameObject.SetActive(true);
            }

            // If the newActiveNodes don't already contain this node, add it
            if (!newActiveNodes.Contains(subNode))
                newActiveNodes.Add(subNode);
        }

        // Discard the parent mesh
        DiscardNode(node);
    }

    /// <summary>
    /// Either creates or shows the already existing mesh corresponding to this node
    /// </summary>
    private void ShowNode(QuadTreeNode<NodeData> node, HashSet<QuadTreeNode<NodeData>> newActiveNodes)
    {
        if (RenderTerrain)
        {
            // If there isn't already a mesh linked to this node
            if (RenderTerrain && !nodeMeshMap.ContainsKey(node))
            {
                // Create an empty mesh holder object
                CachedMeshHolder container = CreateNewBlock(node);

                // Set chunk data...?
                // ...

                // Call highest detail action...?
                // ...

                // Add the newly created mesh to the lists
                activeMeshes.Add(container);
                nodeMeshMap[node] = container;
            }

            // Show the mesh
            nodeMeshMap[node].gameObject.SetActive(true);
        }

        // If the newActiveNodes don't already contain this node, add it
        if (!newActiveNodes.Contains(node))
            newActiveNodes.Add(node);
    }

    /// <summary>
    /// Removes the node from the "active-lists" and deactivates the GameObject
    /// </summary>
    private void DiscardNode(QuadTreeNode<NodeData> node)
    {
        CachedMeshHolder mf;
        if (nodeMeshMap.TryGetValue(node, out mf))
        {
            // Hide and pool node
            activeMeshes.Remove(mf);

            mf.gameObject.SetActive(false);

            // Handle other pooling and task stuff...
            // ...
        }
    }

    /// <summary>
    /// Create a new empty CachedMeshHolder
    /// </summary>
    private CachedMeshHolder CreateNewBlock(QuadTreeNode<NodeData> node)
    {
        GameObject block = GameObject.Instantiate(BlockPrefab);
        block.name = "Block_" + node.Depth;
        block.transform.SetParent(gameObject.transform);
        block.transform.localPosition = node.Position - node.Size * new Vector3(0.5f, 0, 0.5f);
        block.transform.localScale = new Vector3(node.Size, 1, node.Size);

        Mesh blockMesh = meshGenerator.BuildPatch(node.Position, node.Size, ResolutionMultiplier, HeightMap, HeightMultiplier);
        //blockMesh.RecalculateNormals(); // TO DO: place these heavy calculations somewhere else
        blockMesh.RecalculateBounds(); // TO DO: place these heavy calculations somewhere else
        block.GetComponent<MeshFilter>().mesh = blockMesh;
        //block.GetComponent<MeshFilter>().mesh.bounds = new Bounds(Vector3.zero, new Vector3(1, 0.5f, 1) * node.Size * 0.4f);
        block.GetComponent<MeshCollider>().sharedMesh = blockMesh;
        block.GetComponent<MeshRenderer>().material = TerrainMaterial;

        CachedMeshHolder holder = new CachedMeshHolder()
        {
            gameObject = block,
            filter = gameObject.GetComponent<MeshFilter>(),
            collider = gameObject.GetComponent<MeshCollider>(),
            renderer = gameObject.GetComponent<MeshRenderer>()
        };

        return holder;
    }

    /// <summary>
    /// Subdivides and sets subNode data
    /// Note that this is only a theoretical subdivide, it does not create meshes here
    /// </summary>
    public void Subdivide(QuadTreeNode<NodeData> node)
    {
        node.Subdivide();
        foreach (QuadTreeNode<NodeData> subNode in node.SubNodes)
        {
            GenerateNodeData(subNode);
        }
    }

    /// <summary>
    /// Generates new NodeData
    /// </summary>
    public void GenerateNodeData(QuadTreeNode<NodeData> node)
    {
        NodeData data = new NodeData();
        data.Breakpoint = MaxResolutionAt * Mathf.Pow(breakPointEXP, MaxDepth - node.Depth);
        node.Data = data;
    }

    /// <summary>
    /// Returns true if the node is within camera range
    /// </summary>
    public bool CanSplit(QuadTreeNode<NodeData> node)
    {
        
        bool distanceMetricBool = false, bumpMetricBool = false, errorMetricBool = false;
        float distance = Vector3.Distance(Camera.main.transform.position, node.Position);

        if (UseDistanceMetrics)
        {
            // The distance calculation is very computation heavy and should be avoided
            distanceMetricBool = (distance - node.Size) < (node.Data.Breakpoint);
        }

        if(UseBumpMetric)
        {
            // Include depth in the calculation
            float depthCoefficient = 1.0f / (Mathf.Pow(2.0f, (float)node.Depth));
            //float depthCoefficient0 = (MaxDepth - node.Depth) / (float)MaxDepth;

            if ((distance - node.Size) < node.Data.Variance * VarianceScaler * depthCoefficient)
            {
                bumpMetricBool = true;
            }
        }

        if(UseErrorMetric)
        {
            if((distance - node.Size) < node.Data.ErrorMetric)
            {
                errorMetricBool = true;
            }
        }

        return (distanceMetricBool || bumpMetricBool || errorMetricBool);
    }
}