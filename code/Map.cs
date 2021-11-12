using Sandbox;
using System;
using System.Collections.Generic;

namespace Sandblox
{
	public partial class Map : BaseNetworkable
	{
		[Net] public int SizeX { get; private set; }
		[Net] public int SizeY { get; private set; }
		[Net] public int SizeZ { get; private set; }

		[Net] private IList<Chunk> Chunks { get; set; }
		private ChunkData[] ChunkData { get; set; }

		private int numChunksX;
		private int numChunksY;
		private int numChunksZ;

		public int NumChunksX => numChunksX;
		public int NumChunksY => numChunksY;
		public int NumChunksZ => numChunksZ;

		public void SetSize( int sizeX, int sizeY, int sizeZ )
		{
			SizeX = sizeX;
			SizeY = sizeY;
			SizeZ = sizeZ;

			numChunksX = SizeX / Chunk.ChunkSize;
			numChunksY = SizeY / Chunk.ChunkSize;
			numChunksZ = SizeZ / Chunk.ChunkSize;

			ChunkData = new ChunkData[numChunksX * numChunksY * numChunksZ];

			for ( int x = 0; x < numChunksX; ++x )
			{
				for ( int y = 0; y < numChunksY; ++y )
				{
					for ( int z = 0; z < numChunksZ; ++z )
					{
						var chunkIndex = x + y * numChunksX + z * numChunksX * numChunksY;
						var chunk = new ChunkData( new IntVector3( x * Chunk.ChunkSize, y * Chunk.ChunkSize, z * Chunk.ChunkSize ) );
						ChunkData[chunkIndex] = chunk;
					}
				}
			}
		}

		public void Init()
		{
			numChunksX = SizeX / Chunk.ChunkSize;
			numChunksY = SizeY / Chunk.ChunkSize;
			numChunksZ = SizeZ / Chunk.ChunkSize;

			if ( Chunks != null )
			{
				foreach ( var chunk in Chunks )
				{
					if ( chunk == null )
						continue;

					chunk.Map = this;
					chunk.Init();
				}
			}
		}

		private void SpawnChunks()
		{
			foreach ( var chunkData in ChunkData )
			{
				Chunks.Add( new Chunk( this, chunkData ) );
			}
		}

		public bool SetBlockAndUpdate( IntVector3 blockPos, byte blocktype )
		{
			bool build = false;
			var chunkids = new HashSet<int>();

			if ( SetBlock( blockPos, blocktype ) )
			{
				if ( Host.IsServer )
					return true;

				var chunkIndex = GetBlockChunkIndexAtPosition( blockPos );

				chunkids.Add( chunkIndex );

				build = true;

				for ( int i = 0; i < 6; i++ )
				{
					if ( IsAdjacentBlockEmpty( blockPos, i ) )
					{
						var posInChunk = GetBlockPositionInChunk( blockPos );
						Chunks[chunkIndex].UpdateBlockSlice( posInChunk, i );

						continue;
					}

					var adjacentPos = GetAdjacentBlockPosition( blockPos, i );
					var adjadentChunkIndex = GetBlockChunkIndexAtPosition( adjacentPos );
					var adjacentPosInChunk = GetBlockPositionInChunk( adjacentPos );

					chunkids.Add( adjadentChunkIndex );

					Chunks[adjadentChunkIndex].UpdateBlockSlice( adjacentPosInChunk, GetOppositeDirection( i ) );
				}
			}

			foreach ( var chunkid in chunkids )
			{
				Chunks[chunkid].Build();
			}

			return build;
		}

		public int GetBlockChunkIndexAtPosition( IntVector3 pos )
		{
			return (pos.x / Chunk.ChunkSize) + (pos.y / Chunk.ChunkSize) * numChunksX + (pos.z / Chunk.ChunkSize) * numChunksX * numChunksY;
		}

		public static IntVector3 GetBlockPositionInChunk( IntVector3 pos )
		{
			return new IntVector3( pos.x % Chunk.ChunkSize, pos.y % Chunk.ChunkSize, pos.z % Chunk.ChunkSize );
		}

		public static int GetOppositeDirection( int direction ) { return direction + ((direction % 2 != 0) ? -1 : 1); }

		public void GeneratePerlin()
		{
			for ( int x = 0; x < SizeX; ++x )
			{
				for ( int y = 0; y < SizeY; ++y )
				{
					int height = (int)((SizeZ / 2) * (Noise.Perlin( (x * 32) * 0.001f, (y * 32) * 0.001f, 0 ) + 0.5f) * 0.5f);
					if ( height <= 0 ) height = 1;
					if ( height > SizeZ ) height = SizeZ;

					for ( int z = 0; z < SizeZ; ++z )
					{
						SetBlockTypeAtPosition( new IntVector3( x, y, z ), (byte)(z < height ? Rand.Int( 2, 2 ) : 0) );
					}
				}
			}

			SpawnChunks();
		}

		public void GenerateGround()
		{
			for ( int x = 0; x < SizeX; ++x )
			{
				for ( int y = 0; y < SizeY; ++y )
				{
					int height = 10;
					if ( height <= 0 ) height = 1;
					if ( height > SizeZ ) height = SizeZ;

					for ( int z = 0; z < SizeZ; ++z )
					{
						SetBlockTypeAtPosition( new IntVector3( x, y, z ), (byte)(z < height ? Rand.Int( 1, 5 ) : 0) );
					}
				}
			}

			SpawnChunks();
		}

		private void SetBlockTypeAtPosition( IntVector3 pos, byte blockType )
		{
			if ( ChunkData == null )
				return;

			var chunkIndex = GetBlockChunkIndexAtPosition( pos );
			var blockPositionInChunk = GetBlockPositionInChunk( pos );
			var chunk = ChunkData[chunkIndex];

			chunk.SetBlockTypeAtPosition( blockPositionInChunk, blockType );
		}

		public byte GetBlockTypeAtPosition( IntVector3 pos )
		{
			var chunkIndex = GetBlockChunkIndexAtPosition( pos );
			var blockPositionInChunk = GetBlockPositionInChunk( pos );
			var chunk = Chunks[chunkIndex];

			return chunk.GetBlockTypeAtPosition( blockPositionInChunk );
		}

		public bool SetBlock( IntVector3 pos, byte blockType )
		{
			if ( pos.x < 0 || pos.x >= SizeX ) return false;
			if ( pos.y < 0 || pos.y >= SizeY ) return false;
			if ( pos.z < 0 || pos.z >= SizeZ ) return false;

			var chunkIndex = GetBlockChunkIndexAtPosition( pos );
			var blockPositionInChunk = GetBlockPositionInChunk( pos );
			int blockindex = Chunk.GetBlockIndexAtPosition( blockPositionInChunk );
			var chunk = Chunks[chunkIndex];
			int currentBlockType = chunk.GetBlockTypeAtIndex( blockindex );

			if ( blockType == currentBlockType )
			{
				return false;
			}

			if ( (blockType != 0 && currentBlockType == 0) || (blockType == 0 && currentBlockType != 0) )
			{
				chunk.SetBlockTypeAtIndex( blockindex, blockType );

				if ( Host.IsServer )
				{
					var chunkData = ChunkData[chunkIndex];
					chunkData.WriteNetworkData();
				}

				return true;
			}

			return false;
		}

		public static IntVector3 GetAdjacentBlockPosition( IntVector3 pos, int side )
		{
			return pos + Chunk.BlockDirections[side];
		}

		public bool IsAdjacentBlockEmpty( IntVector3 pos, int side )
		{
			return IsBlockEmpty( GetAdjacentBlockPosition( pos, side ) );
		}

		public bool IsBlockEmpty( IntVector3 pos )
		{
			if ( pos.x < 0 || pos.x >= SizeX ||
				 pos.y < 0 || pos.y >= SizeY )
			{
				return true;
			}

			if ( pos.z < 0 || pos.z >= SizeZ )
			{
				return true;
			}

			if ( pos.z >= SizeZ )
			{
				return true;
			}

			var chunkIndex = GetBlockChunkIndexAtPosition( pos );
			var blockPositionInChunk = GetBlockPositionInChunk( pos );
			var chunk = Chunks[chunkIndex];

			return chunk.GetBlockTypeAtPosition( blockPositionInChunk ) == 0;
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

				byte blockType = GetBlockTypeAtPosition( position3i );

				if ( blockType != 0 )
				{
					hitPosition = position3i;

					return lastFace;
				}
			}

			Plane plane = new( new Vector3( 0.0f, 0.0f, 0.0f ), new Vector3( 0.0f, 0.0f, 1.0f ) );
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

				byte blockType = GetBlockTypeAtPosition( blockHitPosition );

				if ( blockType == 0 )
				{
					distance = distanceHit;
					hitPosition = blockHitPosition;
					hitPosition.z = -1;

					return BlockFace.Top;
				}
			}

			// made it all the way there
			distance = length;

			return BlockFace.Invalid;
		}
	}
}
