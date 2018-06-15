// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MIConvexHull;
using System.Linq;

namespace Academy.HoloToolkit.Unity
{
    /// <summary>
    /// RemoveSurfaceVertices will remove any vertices from the Spatial Mapping Mesh that fall within the bounding volume.
    /// This can be used to create holes in the environment, or to help reduce triangle count after finding planes.
    /// </summary>
    public class RemoveSurfaceVertices : Singleton<RemoveSurfaceVertices>
    {

        struct BoundsContainer
        {
            public Bounds bounds;
            public bool createMesh;
        };

        [Tooltip("The amount, if any, to expand each bounding volume by.")]
        public float BoundsExpansion = 0.0f;

        [Tooltip("The amount, if any, to expand the convex hull by.")]
        public float ConvexHullExpansion = 0.1f;

        /// <summary>
        /// Delegate which is called when the RemoveVerticesComplete event is triggered.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="args"></param>
        public delegate void EventHandler(object source, EventArgs args);

        /// <summary>
        /// EventHandler which is triggered when the RemoveSurfaceVertices is finished.
        /// </summary>
        public event EventHandler RemoveVerticesComplete;

        /// <summary>
        /// Indicates if RemoveSurfaceVertices is currently removing vertices from the Spatial Mapping Mesh.
        /// </summary>
        private bool removingVerts = false;

        /// <summary>
        /// Queue of bounding objects to remove surface vertices from.
        /// Bounding objects are queued so that RemoveSurfaceVerticesWithinBounds can be called even when the previous task has not finished.
        /// </summary>
        private Queue<BoundsContainer> boundingObjectsQueue;

#if UNITY_EDITOR || UNITY_STANDALONE
        /// <summary>
        /// How much time (in sec), while running in the Unity Editor, to allow RemoveSurfaceVertices to consume before returning control to the main program.
        /// </summary>
        private static readonly float FrameTime = .016f;
#else
        /// <summary>
        /// How much time (in sec) to allow RemoveSurfaceVertices to consume before returning control to the main program.
        /// </summary>
        private static readonly float FrameTime = .008f;
#endif

        public Material removedObjectMaterial;
        public Material removedObjectWireframeMaterial;
        [Tooltip("The projective texture mapping material.")]
        public Material PTMMaterial;
        private bool didCreateRemovedObject = false;

        // GameObject initialization.
        private void Start()
        {
            boundingObjectsQueue = new Queue<BoundsContainer>();
            removingVerts = false;
        }

        /// <summary>
        /// Removes portions of the surface mesh that exist within the bounds of the boundingObjects.
        /// </summary>
        /// <param name="boundingObjects">Collection of GameObjects that define the bounds where spatial mesh vertices should be removed.</param>
        public void RemoveSurfaceVerticesWithinBounds(IEnumerable<GameObject> boundingObjects)
        {
            if (boundingObjects == null) return;

            if (!removingVerts) {
                removingVerts = true;
                AddBoundingObjectsToQueue(boundingObjects, false);

                // We use Coroutine to split the work across multiple frames and avoid impacting the frame rate too much.
                StartCoroutine(RemoveSurfaceVerticesWithinBoundsRoutine());
            } else {
                // Add new boundingObjects to end of queue.
                AddBoundingObjectsToQueue(boundingObjects, false);
            }
        }

        /// <summary>
        /// Removes portions of the surface mesh that exist within the bounds of the boundingObjects.
        /// </summary>
        /// <param name="boundingObjects">Collection of GameObjects that define the bounds where spatial mesh vertices should be removed.</param>
        public void RemoveSurfaceVerticesWithinBoundsAndGenerateMesh(IEnumerable<GameObject> boundingObjects)
        {
            if (boundingObjects == null) return;

            if (!removingVerts) {
                removingVerts = true;
                AddBoundingObjectsToQueue(boundingObjects, true);

                // We use Coroutine to split the work across multiple frames and avoid impacting the frame rate too much.
                StartCoroutine(RemoveSurfaceVerticesWithinBoundsRoutine());
            } else {
                // Add new boundingObjects to end of queue.
                AddBoundingObjectsToQueue(boundingObjects, true);
            }
        }

        /// <summary>
        /// Adds new bounding objects to the end of the Queue.
        /// </summary>
        /// <param name="boundingObjects">Collection of GameObjects which define the bounds where spatial mesh vertices should be removed.</param>
        private void AddBoundingObjectsToQueue(IEnumerable<GameObject> boundingObjects, bool createMesh)
        {
            foreach (GameObject item in boundingObjects)
            {
                Bounds bounds = new Bounds();

                Collider boundingCollider = item.GetComponent<Collider>();
                if (boundingCollider != null)
                {
                    bounds = boundingCollider.bounds;

                    // Expand the bounds, if requested.
                    if (BoundsExpansion > 0.0f)
                    {
                        bounds.Expand(BoundsExpansion);
                    }

                    BoundsContainer container = new BoundsContainer();
                    container.bounds = bounds;
                    container.createMesh = createMesh;
                    boundingObjectsQueue.Enqueue(container);
                }
            }
        }

        /// <summary>
        /// Iterator block, analyzes surface meshes to find vertices existing within the bounds of any boundingObject and removes them.
        /// </summary>
        /// <returns>Yield result.</returns>
        private IEnumerator RemoveSurfaceVerticesWithinBoundsRoutine()
        {
            List<MeshFilter> meshFilters = SpatialMappingManager.Instance.GetMeshFilters();
            float start = Time.realtimeSinceStartup;
            
            List<Vector3> removedObjectVertices = new List<Vector3>();
            List<Vector3> removedObjectNormals = new List<Vector3>();
            List<int> removedObjectIndices = new List<int>();

            while (boundingObjectsQueue.Count > 0)
            {
                // Get the current boundingObject.
                BoundsContainer container = boundingObjectsQueue.Dequeue();
                Bounds bounds = container.bounds;

                foreach (MeshFilter filter in meshFilters)
                {
                    // Since this is amortized across frames, the filter can be destroyed by the time
                    // we get here.
                    if (filter == null) continue;

                    Mesh mesh = filter.sharedMesh;
                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

                    // The mesh renderer bounds are in world space.
                    // If the mesh is null there is nothing to process
                    // If the renderer is null we can't get the renderer bounds
                    // If the renderer's bounds aren't contained inside of the current
                    // bounds from the bounds queue there is no reason to process
                    // If any of the above conditions are met, then we should go to the next meshfilter. 
                    if (mesh == null || renderer == null || !renderer.bounds.Intersects(bounds)) continue;

                    // Remove vertices from any mesh that intersects with the bounds.
                    Vector3[] verts = mesh.vertices;
                    List<int> vertsToRemove = new List<int>();

                    // Find which mesh vertices are within the bounds.
                    for (int i = 0; i < verts.Length; ++i) {
                        if (bounds.Contains(filter.transform.TransformPoint(verts[i]))) {
                            // These vertices are within bounds, so mark them for removal.
                            vertsToRemove.Add(i);
                        }

                        // If too much time has passed, we need to return control to the main game loop.
                        if ((Time.realtimeSinceStartup - start) > FrameTime) {
                            // Pause our work here, and continue finding vertices to remove on the next frame.
                            yield return null;
                            start = Time.realtimeSinceStartup;
                        }
                    }

                    // If we did not find any vertices to remove, continue to the next mesh.
                    if (vertsToRemove.Count == 0) continue;

                    // We found vertices to remove, so now we need to remove any triangles that reference these vertices.
                    int[] indices = mesh.GetTriangles(0);
                    List<int> updatedIndices = new List<int>();
                    List<int> removedIndices = new List<int>();
                    List<int> boundaryVertices = new List<int>();

                    for (int index = 0; index < indices.Length; index += 3) {
                        // Each triangle utilizes three slots in the index buffer, check to see if any of the
                        // triangle indices contain a vertex that should be removed.
                        int indexA = indices[index];
                        int indexB = indices[index + 1];
                        int indexC = indices[index + 2];
                        bool containsA = vertsToRemove.Contains(indexA);
                        bool containsB = vertsToRemove.Contains(indexB);
                        bool containsC = vertsToRemove.Contains(indexC);
                        if (containsA || containsB || containsC) {
                            // Do nothing, we don't want to save this triangle...
                            // Instead, keep track of it for the removed mesh
                            removedIndices.Add(indexA);
                            removedIndices.Add(indexB);
                            removedIndices.Add(indexC);
                            // Also add any vertices that will be needed to reproduce the triangles in the removed mesh
                            if (!containsA && !boundaryVertices.Contains(indexA)) boundaryVertices.Add(indexA);
                            if (!containsB && !boundaryVertices.Contains(indexB)) boundaryVertices.Add(indexB);
                            if (!containsC && !boundaryVertices.Contains(indexC)) boundaryVertices.Add(indexC);
                        } else {
                            // Every vertex in this triangle is good, so let's save it.
                            updatedIndices.Add(indexA);
                            updatedIndices.Add(indexB);
                            updatedIndices.Add(indexC);
                        }

                        // If too much time has passed, we need to return control to the main game loop.
                        if ((Time.realtimeSinceStartup - start) > FrameTime) {
                            // Pause our work, and continue making additional planes on the next frame.
                            yield return null;
                            start = Time.realtimeSinceStartup;
                        }
                    }

                    // If none of the verts to remove were being referenced in the triangle list, continue
                    if (indices.Length == updatedIndices.Count) continue;

                    // Update mesh to use the new triangles.
                    mesh.SetTriangles(updatedIndices.ToArray(), 0);
                    mesh.RecalculateBounds();
                    yield return null;
                    start = Time.realtimeSinceStartup;

                    if (container.createMesh) {
                        /* SETUP BUFFERS FOR REMOVED MESH */
                        SortedDictionary<int, int> vertexMap = new SortedDictionary<int, int>();
                        //List<int> remappedIndices = new List<int>();
                        vertsToRemove.AddRange(boundaryVertices);
                        vertsToRemove.Sort();
                        // Use a dictionary to allow faster remapping of indices
                        for (int k = 0; k < vertsToRemove.Count; k++)
                        {
                            int index = vertsToRemove[k];
                            vertexMap.Add(index, k + removedObjectVertices.Count);
                        }
                        // Add vertices to removed mesh
                        for (int k = 0; k < vertsToRemove.Count; k++)
                        {
                            int index = vertsToRemove[k];
                            Vector3 vertex = filter.transform.localToWorldMatrix.MultiplyPoint(verts[index]);
                            removedObjectVertices.Add(vertex);
                            Vector3 normal = mesh.normals[index];
                            removedObjectNormals.Add(normal);
                        }
                        // Add remapped indices to removed mesh
                        for (int k = 0; k < removedIndices.Count; k++)
                        {
                            int oldIndex = removedIndices[k];
                            int newIndex = vertexMap[oldIndex];
                            removedObjectIndices.Add(newIndex);
                        }

                        yield return null;
                        start = Time.realtimeSinceStartup;
                    }

                    // Reset the mesh collider to fit the new mesh.
                    MeshCollider collider = filter.gameObject.GetComponent<MeshCollider>();
                    if (collider != null) {
                        collider.sharedMesh = null;
                        collider.sharedMesh = mesh;
                    }
                }

                if (container.createMesh) {
                    // Create a GameObject from the removed vertices
                    createRemovedObject(removedObjectVertices, removedObjectNormals, removedObjectIndices);
                }
            }

            Debug.Log("Finished removing vertices.");

            // We are done removing vertices, trigger an event.
            EventHandler handler = RemoveVerticesComplete;
            if (handler != null) {
                handler(this, EventArgs.Empty);
            }

            removingVerts = false;
        }

        GameObject createRemovedObject(List<Vector3> vertices, List<Vector3> normals, List<int> indices) {
            GameObject removedObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            removedObject.AddComponent<MeshFilter>();
            removedObject.AddComponent<MeshCollider>();
            removedObject.AddComponent<MeshRenderer>();
            //GameObject removedObject = new GameObject("RemovedObject", typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer));
            //MeshFilter removedObjectMeshFilter = removedObject.GetComponent<MeshFilter>();
            MeshRenderer removedObjectMeshRenderer = removedObject.GetComponent<MeshRenderer>();
            //MeshCollider removedObjectMeshCollider = removedObject.GetComponent<MeshCollider>();
            Mesh removedObjectMesh = new Mesh();
            removedObjectMesh.SetVertices(vertices);
            removedObjectMesh.SetNormals(normals);
            removedObjectMesh.SetTriangles(indices.ToArray(), 0);

            // Update the mesh of the removed object
            //var convexHullMesh = getConvexHullMesh(vertices);
            //removedObjectMeshFilter.mesh = convexHullMesh;
            //removedObjectMeshCollider.sharedMesh = convexHullMesh;
            removedObjectMeshRenderer.material = removedObjectMaterial;
            Debug.Log("Finished creating removed mesh.");

            // Remove all vertices within the computed convex hull mesh
            didCreateRemovedObject = true;
            RemoveSurfaceVerticesWithinBounds(new List<GameObject>() { removedObject });
            Debug.Log("Finished removing vertices within convex hull");

            // Create a copy of the removed object to display a wireframe of it
            GameObject removedObject2 = new GameObject("RemovedObject2", typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer));
            MeshFilter removedObject2MeshFilter = removedObject2.GetComponent<MeshFilter>();
            MeshRenderer removedObject2MeshRenderer = removedObject2.GetComponent<MeshRenderer>();
            MeshCollider removedObject2MeshCollider = removedObject2.GetComponent<MeshCollider>();
            removedObject2MeshFilter.mesh = removedObjectMesh;
            removedObject2MeshCollider.sharedMesh = removedObjectMesh;
            removedObject2MeshRenderer.material = removedObjectWireframeMaterial;
            removedObject2MeshRenderer.material.SetColor("_WireColor", new Color(1, 0, 216.0f / 255.0f, 1));
            removedObject2MeshRenderer.material.SetColor("_BaseColor", new Color(1, 1, 1, 1));
            removedObject2MeshRenderer.material.SetFloat("_WireThickness", 400.0f);

            // Create a plane underneath the selected object
            //var lowestVertex = new Vector3(0, 100, 0);
            //foreach (var vertex in convexHullMesh.vertices) {
            //    if (vertex.y < lowestVertex.y) lowestVertex = vertex;
            //}
            //GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            //floor.name = "Floor";
            //floor.AddComponent<MeshFilter>();
            //floor.AddComponent<MeshCollider>();
            //floor.AddComponent<MeshRenderer>();
            //MeshRenderer floorMeshRenderer = floor.GetComponent<MeshRenderer>();
            //floorMeshRenderer.material = PTMMaterial;
            //floor.transform.position = lowestVertex + new Vector3(0, 0.03f, 0);
            //floor.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            return removedObject;
        }

        Mesh getConvexHullMesh(IEnumerable<Vector3> points)
        {
            Mesh mesh = new Mesh();
            List<int> triangles = new List<int>();

            // Convert vec3s to IVectors
            var verts = points.Select(x => new MIVertex(x)).ToList();

            // Find convex hull
            var convexHull = MIConvexHull.ConvexHull.Create(verts);

            // Extract triangle indices
            var convexPoints = convexHull.Points.ToList();
            foreach (var face in convexHull.Faces)
            {
                triangles.Add(convexPoints.IndexOf(face.Vertices[0]));
                triangles.Add(convexPoints.IndexOf(face.Vertices[1]));
                triangles.Add(convexPoints.IndexOf(face.Vertices[2]));
            }

            Vector3 averagePos = new Vector3(0, 0, 0);
            Vector3[] vertices = convexHull.Points.Select(x => x.ToVec()).ToArray();
            // Compute average position
            foreach (var vertex in vertices) {
                averagePos += vertex;
            }
            averagePos /= vertices.Length;
            // Expand convex hull
            for (int i = 0; i < vertices.Length; i++) {
                vertices[i] += (vertices[i] - averagePos).normalized * ConvexHullExpansion;
            }
            
            // Update the mesh object and compute normals
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}