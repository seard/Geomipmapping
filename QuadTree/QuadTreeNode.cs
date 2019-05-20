using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The quad index enum which simplifies node links
/// </summary>
public enum QuadTreeIndex
{
    TopLeft = 0,        // 00
    TopRight = 1,       // 01
    BottomLeft = 2,     // 10
    BottomRight = 3,    // 11
}

public class QuadTreeNode<T>
{
    public Vector3 Position;
    public float Size;
    public int Depth;
    public QuadTreeNode<T>[] SubNodes;
    public QuadTreeNode<T> Parent;
    public T Data;

    public QuadTreeNode(QuadTreeNode<T> parent, Vector3 position, float size, int depth, T data = default(T))
    {
        Position = position;
        Size = size;
        Depth = depth;
        Parent = parent;
        Data = data;
    }

    public void Subdivide()
    {
        SubNodes = new QuadTreeNode<T>[4];

        for (int i = 0; i < SubNodes.Length; i++)
        {
            Vector3 newPos = Position;

            if ((i & 2) == 2)
                newPos.z -= Size * 0.25f;
            else
                newPos.z += Size * 0.25f;

            if ((i & 1) == 1)
                newPos.x += Size * 0.25f;
            else
                newPos.x -= Size * 0.25f;

            SubNodes[i] = new QuadTreeNode<T>(this, newPos, Size * 0.5f, Depth + 1);
        }
    }

    /*
    public void GradientSubdivide(LinkedList<QuadTreeNode<T>> selectedNodes, Vector3 targetPosition, float radius, int depth = 0)
    {
        // This only runs for the lowest depth nodes
        if (depth == 0)
        {
            selectedNodes.AddLast(this);
            return;
        }

        var subdivIndex = GetIndexOfPosition(targetPosition, Position);

        if (IsLeaf())
        {
            SubNodes = new QuadTreeNode<T>[4];

            for (int i = 0; i < SubNodes.Length; i++)
            {
                Vector3 newPos = Position;

                if ((i & 2) == 2)
                    newPos.z -= Size * 0.25f;
                else
                    newPos.z += Size * 0.25f;

                if ((i & 1) == 1)
                    newPos.x += Size * 0.25f;
                else
                    newPos.x -= Size * 0.25f;

                SubNodes[i] = new QuadTreeNode<T>(this, newPos, Size * 0.5f, depth - 1);
            }
        }

        for (int i = 0; i < SubNodes.Length; i++)
        {
            int range = ContainedInRange(targetPosition, radius, 3);
            if (depth > range)
            {
                SubNodes[i].GradientSubdivide(selectedNodes, targetPosition, radius, depth - 1);
            }
        }
    }
    */

    public bool IsLeaf
    {
        get { return SubNodes == null; }
    }

    private static int GetIndexOfPosition(Vector3 lookupPosition, Vector3 nodePosition)
    {
        int index = 0;

        index |= lookupPosition.z < nodePosition.z ? 2 : 0;
        index |= lookupPosition.x > nodePosition.x ? 1 : 0;

        return index;
    }
}
/*
public static class PlaneConstructor<T>
{
    public static GameObject CreatePlane(QuadTreeNode<T> owner, Vector3 position, float size, int depth)
    {
        GameObject patch = GameObject.Instantiate(Resources.Load("Patch") as GameObject);
        patch.GetComponent<BlockToNodeLinker>().Owner = owner;
        patch.transform.position = new Vector3(position.x, 0, position.z);
        patch.transform.localScale = Vector3.one * size / 10;
        return patch;
    }
}
*/