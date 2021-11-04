using Sandbox;
using System;

namespace Sandblox
{
	public class Map
	{
		private readonly byte[] blockdata = null;
		private readonly byte[] healthdata = null;

		private readonly static int[] xOffsets = new[] { 0, 0, 0, 0, -1, 1 };
		private readonly static int[] yOffsets = new[] { 0, 0, -1, 1, 0, 0 };
		private readonly static int[] zOffsets = new[] { 1, -1, 0, 0, 0, 0 };

		private readonly int sizeX;
		private readonly int sizeY;
		private readonly int sizeZ;

		public int SizeX => sizeX;
		public int SizeY => sizeY;
		public int SizeZ => sizeZ;

		public Map( int sizeX, int sizeY, int sizeZ )
		{
			this.sizeX = sizeX;
			this.sizeY = sizeY;
			this.sizeZ = sizeZ;

			blockdata = new byte[this.sizeX * this.sizeY * this.sizeZ];
			healthdata = new byte[this.sizeX * this.sizeY * this.sizeZ];
		}

		public void GeneratePerlin()
		{
			for ( int x = 0; x < sizeX; ++x )
			{
				for ( int y = 0; y < sizeY; ++y )
				{
					int height = (int)((sizeZ / 2) * (Noise.Perlin( (x * 32) * 0.001f, (y * 32) * 0.001f, 0 ) + 0.5f) * 0.5f);
					if ( height <= 0 ) height = 1;
					if ( height > sizeZ ) height = sizeZ;

					for ( int z = 0; z < sizeZ; ++z )
					{
						int blockIndex = GetBlockIndex( x, y, z );
						blockdata[blockIndex] = (byte)(z < height ? (Rand.Int( 2, 2 )) : 0);
						healthdata[blockIndex] = (byte)Rand.Int( 1, 15 );
					}
				}
			}
		}

		public void GenerateGround()
		{
			for ( int x = 0; x < sizeX; ++x )
			{
				for ( int y = 0; y < sizeY; ++y )
				{
					int height = 10;
					if ( height <= 0 ) height = 1;
					if ( height > sizeZ ) height = sizeZ;

					for ( int z = 0; z < sizeZ; ++z )
					{
						int blockIndex = GetBlockIndex( x, y, z );
						blockdata[blockIndex] = (byte)(z < height ? (Rand.Int( 1, 5 )) : 0);
						healthdata[blockIndex] = (byte)Rand.Int( 1, 255 );
					}
				}
			}
		}

		public bool SetBlock( int x, int y, int z, byte blocktype, byte health = 15 )
		{
			if ( x < 0 || x >= sizeX ) return false;
			if ( y < 0 || y >= sizeY ) return false;
			if ( z < 0 || z >= sizeZ ) return false;

			int blockindex = GetBlockIndex( x, y, z );
			int curBlocktype = GetBlockData( blockindex );

			if ( blocktype == curBlocktype && health == healthdata[blockindex] )
			{
				return false;
			}

			if ( (blocktype != 0 && curBlocktype == 0) || (blocktype == 0 && curBlocktype != 0) )
			{
				blockdata[blockindex] = blocktype;
				healthdata[blockindex] = health;

				return true;
			}

			return false;
		}

		public static IntVector3 GetAdjacentPos( int x, int y, int z, int side )
		{
			int adjacentX = x + xOffsets[side];
			int adjacentY = y + yOffsets[side];
			int adjacentZ = z + zOffsets[side];

			return new IntVector3( adjacentX, adjacentY, adjacentZ );
		}

		public bool IsAdjacentBlockEmpty( int x, int y, int z, int side )
		{
			int adjacentX = x + xOffsets[side];
			int adjacentY = y + yOffsets[side];
			int adjacentZ = z + zOffsets[side];

			if ( adjacentX < 0 || adjacentX >= sizeX ||
				 adjacentY < 0 || adjacentY >= sizeY )
			{
				return true;
			}

			if ( adjacentZ < 0 || adjacentZ >= sizeZ )
			{
				return true;
			}

			if ( adjacentZ >= sizeZ )
			{
				return true;
			}

			var blockIndex = GetBlockIndex( adjacentX, adjacentY, adjacentZ );
			return blockdata[blockIndex] == 0;
		}

		public bool IsBlockEmpty( int x, int y, int z )
		{
			if ( x < 0 || x >= sizeX ||
				 y < 0 || y >= sizeY )
			{
				return true;
			}

			if ( z < 0 || z >= sizeZ )
			{
				return true;
			}

			if ( z >= sizeZ )
			{
				return true;
			}

			var blockIndex = GetBlockIndex( x, y, z );
			return blockdata[blockIndex] == 0;
		}

		public int GetBlockIndex( int x, int y, int z )
		{
			return x + y * sizeX + z * sizeX * sizeY;
		}

		public byte GetBlockData( int x, int y, int z )
		{
			return blockdata[GetBlockIndex( x, y, z )];
		}

		public byte GetBlockData( int index )
		{
			return blockdata[index];
		}

		public byte GetBlockBrightness( int index )
		{
			return healthdata[index];
		}

		public enum BlockFace : int
		{
			Invalid = -1,
			Top = 0,
			Bottom = 1,
			West = 2,
			East = 3,
			South = 4,
			North = 5,
		};

		public BlockFace GetBlockInDirection( Vector3 position, Vector3 direction, float length, out IntVector3 hitPosition, out float distance )
		{
			hitPosition = new IntVector3( 0, 0, 0 );
			distance = 0;

			if ( direction.Length <= 0.0f )
			{
				return BlockFace.Invalid;
			}

			// distance from block position to edge of block
			IntVector3 edgeOffset = new( direction.x < 0 ? 0 : 1,
								direction.y < 0 ? 0 : 1,
								direction.z < 0 ? 0 : 1 );

			// amount to step in each direction
			IntVector3 stepAmount = new( direction.x < 0 ? -1 : 1,
								direction.y < 0 ? -1 : 1,
								direction.z < 0 ? -1 : 1 );

			// face that will be hit in each direction
			IntVector3 faceDirection = new( direction.x < 0 ? (int)BlockFace.North : (int)BlockFace.South,
								   direction.y < 0 ? (int)BlockFace.East : (int)BlockFace.West,
								   direction.z < 0 ? (int)BlockFace.Top : (int)BlockFace.Bottom );

			Vector3 position3f = position; // start position
			distance = 0; // distance from starting position
			Ray ray = new( position, direction );

			while ( true )
			{
				IntVector3 position3i = new( (int)position3f.x, (int)position3f.y, (int)position3f.z ); // position of the block we are in

				// distance from current position to edge of block we are in
				Vector3 distanceToNearestEdge = new( position3i.x - position3f.x + edgeOffset.x,
												   position3i.y - position3f.y + edgeOffset.y,
												   position3i.z - position3f.z + edgeOffset.z );

				// if we are touching an edge, we are 1 unit away from the next edge
				for ( int i = 0; i < 3; ++i )
				{
					if ( MathF.Abs( distanceToNearestEdge[i] ) == 0.0f )
					{
						distanceToNearestEdge[i] = stepAmount[i];
					}
				}

				// length we must travel along the vector to reach the nearest edge in each direction
				Vector3 lengthToNearestEdge = new( MathF.Abs( distanceToNearestEdge.x / direction.x ),
												 MathF.Abs( distanceToNearestEdge.y / direction.y ),
												 MathF.Abs( distanceToNearestEdge.z / direction.z ) );

				int axis;

				// if the nearest edge in the x direction is the closest
				if ( lengthToNearestEdge.x < lengthToNearestEdge.y && lengthToNearestEdge.x < lengthToNearestEdge.z )
				{
					axis = 0;
				}
				// if the nearest edge in the y direction is the closest
				else if ( lengthToNearestEdge.y < lengthToNearestEdge.x && lengthToNearestEdge.y < lengthToNearestEdge.z )
				{
					axis = 1;
				}
				// if nearest edge in the z direction is the closest
				else
				{
					axis = 2;
				}

				distance += lengthToNearestEdge[axis];
				position3f = position + direction * distance;
				position3f[axis] = MathF.Floor( position3f[axis] + 0.5f * stepAmount[axis] );

				if ( position3f.x < 0.0f || position3f.y < 0.0f || position3f.z < 0.0f ||
					 position3f.x >= SizeX || position3f.y >= SizeY || position3f.z >= SizeZ )
				{
					break;
				}

				// last face hit
				BlockFace lastFace = (BlockFace)faceDirection[axis];

				// if we reached the length cap, exit
				if ( distance > length )
				{
					// made it all the way there
					distance = length;

					return BlockFace.Invalid;
				}

				// if there is a block at the current position, we have an intersection
				position3i = new( (int)position3f.x, (int)position3f.y, (int)position3f.z );

				byte blockType = GetBlockData( position3i.x, position3i.y, position3i.z );

				if ( blockType != 0 )
				{
					hitPosition = position3i;

					return lastFace;
				}
			}

			Plane plane = new( new Vector3( 0.0f, 0.0f, 0.0f ), new Vector3( 0.0f, 1.0f, 0.0f ) );
			float distanceHit = 0;
			var traceHitPos = plane.Trace( ray, true );
			if ( traceHitPos.HasValue ) distanceHit = Vector3.DistanceBetween( position, traceHitPos.Value );

			if ( distanceHit >= 0.0f && distanceHit <= length )
			{
				Vector3 hitPosition3f = position + direction * distanceHit;

				if ( hitPosition3f.x < 0.0f || hitPosition3f.y < 0.0f || hitPosition3f.z < 0.0f ||
					 hitPosition3f.x > SizeX || hitPosition3f.y > SizeY || hitPosition3f.z > SizeZ )
				{
					// made it all the way there
					distance = length;

					return BlockFace.Invalid;
				}

				hitPosition3f.z = 0.0f;
				IntVector3 blockHitPosition = new( (int)hitPosition3f.x, (int)hitPosition3f.y, (int)hitPosition3f.z );

				byte blockType = GetBlockData( blockHitPosition.x, blockHitPosition.y, blockHitPosition.z );

				if ( blockType == 0 )
				{
					distance = distanceHit;
					hitPosition = blockHitPosition;
					hitPosition.y = -1;

					return BlockFace.Top;
				}
			}

			// made it all the way there
			distance = length;

			return BlockFace.Invalid;
		}
	}
}
