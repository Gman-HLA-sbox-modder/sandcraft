﻿using Sandbox;
using System.Collections.Generic;

namespace Sandblox
{
	public class ChunkSlice
	{
		public ChunkSlice()
		{
			body = PhysicsWorld.WorldBody;
		}

		public bool dirty = false;
		public List<BlockVertex> vertices = new();
		public List<Vector3> collisionVertices = new();
		public List<int> collisionIndices = new();
		public PhysicsBody body;
		public PhysicsShape shape;
	}
}
