using Sandbox;
using System;
using System.Collections.Generic;

namespace Sandblox
{
	public class MapDesc : BaseNetworkable, INetworkSerializer
	{
		public ushort sizeX;
		public ushort sizeY;
		public ushort sizeZ;
		public byte[] blockTypes;

		public void Read( ref NetRead read )
		{
			sizeX = read.Read<ushort>();
			sizeY = read.Read<ushort>();
			sizeZ = read.Read<ushort>();
			blockTypes = read.ReadUnmanagedArray( blockTypes );
		}

		public void Write( NetWrite write )
		{
			write.Write( sizeX );
			write.Write( sizeY );
			write.Write( sizeZ );
			write.WriteUnmanagedArray( blockTypes );
		}
	}

	public class Map
	{
		private int SizeX => Desc.sizeX;
		private int SizeY => Desc.sizeY;
		private int SizeZ => Desc.sizeZ;

		private readonly int numChunksX;
		private readonly int numChunksY;
		private readonly int numChunksZ;

		public int NumChunksX => numChunksX;
		public int NumChunksY => numChunksY;
		public int NumChunksZ => numChunksZ;

		private readonly MapDesc Desc;
		private readonly Chunk[] chunks;

		public Map( int sizeX, int sizeY, int sizeZ )
		{
			Desc = new()
			{
				sizeX = (ushort)sizeX,
				sizeY = (ushort)sizeY,
				sizeZ = (ushort)sizeZ,
			};

			Desc.blockTypes = new byte[SizeX * SizeY * SizeZ];

			numChunksX = sizeX / Chunk.ChunkSize;
			numChunksY = sizeY / Chunk.ChunkSize;
			numChunksZ = sizeZ / Chunk.ChunkSize;

			chunks = new Chunk[numChunksX * numChunksY * numChunksZ];
		}

		public void Init()
		{
			for ( int x = 0; x < numChunksX; ++x )
			{
				for ( int y = 0; y < numChunksY; ++y )
				{
					for ( int z = 0; z < numChunksZ; ++z )
					{
						var chunkIndex = x + y * numChunksX + z * numChunksX * numChunksY;
						var chunk = new Chunk( this, new IntVector3( x * Chunk.ChunkSize, y * Chunk.ChunkSize, z * Chunk.ChunkSize ) );
						chunks[chunkIndex] = chunk;
					}
				}
			}
		}

		public void Destroy()
		{
			if ( chunks != null )
			{
				foreach ( var chunk in chunks )
				{
					if ( chunk == null )
						continue;

					chunk.Delete();
				}
			}
		}

		public bool SetBlock( Vector3 pos, Vector3 dir, byte blocktype )
		{
			var face = GetBlockInDirection( pos * (1.0f / 32.0f), dir.Normal, 10000, out var hitpos, out _ );
			if ( face == BlockFace.Invalid )
				return false;

			var blockPos = hitpos;

			if ( blocktype != 0 )
			{
				blockPos = GetAdjacentPos( blockPos, (int)face );
			}

			bool build = false;
			var chunkids = new HashSet<int>();

			if ( SetBlock( blockPos, blocktype ) )
			{
				var chunkIndex = GetBlockChunkIndex( blockPos );

				chunkids.Add( chunkIndex );

				build = true;

				for ( int i = 0; i < 6; i++ )
				{
					if ( IsAdjacentBlockEmpty( blockPos, i ) )
					{
						var posInChunk = GetBlockPosInChunk( blockPos );
						chunks[chunkIndex].UpdateBlockSlice( posInChunk, i );

						continue;
					}

					var adjacentPos = GetAdjacentPos( blockPos, i );
					var adjadentChunkIndex = GetBlockChunkIndex( adjacentPos );
					var adjacentPosInChunk = GetBlockPosInChunk( adjacentPos );

					chunkids.Add( adjadentChunkIndex );

					chunks[adjadentChunkIndex].UpdateBlockSlice( adjacentPosInChunk, GetOppositeDirection( i ) );
				}
			}

			foreach ( var chunkid in chunkids )
			{
				chunks[chunkid].Build();
			}

			return build;
		}

		public int GetBlockChunkIndex( IntVector3 pos )
		{
			return (pos.x / Chunk.ChunkSize) + (pos.y / Chunk.ChunkSize) * numChunksX + (pos.z / Chunk.ChunkSize) * numChunksX * numChunksY;
		}

		public static IntVector3 GetBlockPosInChunk( IntVector3 pos )
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
						int blockIndex = GetBlockIndex( new IntVector3( x, y, z ) );
						Desc.blockTypes[blockIndex] = (byte)(z < height ? (Rand.Int( 2, 2 )) : 0);
					}
				}
			}
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
						int blockIndex = GetBlockIndex( new IntVector3( x, y, z ) );
						Desc.blockTypes[blockIndex] = (byte)(z < height ? (Rand.Int( 1, 5 )) : 0);
					}
				}
			}
		}

		public bool SetBlock( IntVector3 pos, byte blocktype )
		{
			if ( pos.x < 0 || pos.x >= SizeX ) return false;
			if ( pos.y < 0 || pos.y >= SizeY ) return false;
			if ( pos.z < 0 || pos.z >= SizeZ ) return false;

			int blockindex = GetBlockIndex( pos );
			int curBlocktype = GetBlockData( blockindex );

			if ( blocktype == curBlocktype )
			{
				return false;
			}

			if ( (blocktype != 0 && curBlocktype == 0) || (blocktype == 0 && curBlocktype != 0) )
			{
				Desc.blockTypes[blockindex] = blocktype;

				return true;
			}

			return false;
		}

		public static IntVector3 GetAdjacentPos( IntVector3 pos, int side )
		{
			return pos + Chunk.BlockDirections[side];
		}

		public bool IsAdjacentBlockEmpty( IntVector3 pos, int side )
		{
			var adjacentPos = GetAdjacentPos( pos, side );

			if ( adjacentPos.x < 0 || adjacentPos.x >= SizeX ||
				 adjacentPos.y < 0 || adjacentPos.y >= SizeY )
			{
				return true;
			}

			if ( adjacentPos.z < 0 || adjacentPos.z >= SizeZ )
			{
				return true;
			}

			if ( adjacentPos.z >= SizeZ )
			{
				return true;
			}

			var blockIndex = GetBlockIndex( adjacentPos );
			return Desc.blockTypes[blockIndex] == 0;
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

			var blockIndex = GetBlockIndex( pos );
			return Desc.blockTypes[blockIndex] == 0;
		}

		public int GetBlockIndex( IntVector3 pos )
		{
			return pos.x + pos.y * SizeX + pos.z * SizeX * SizeY;
		}

		public byte GetBlockData( IntVector3 pos )
		{
			return Desc.blockTypes[GetBlockIndex( pos )];
		}

		public byte GetBlockData( int index )
		{
			return Desc.blockTypes[index];
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

				byte blockType = GetBlockData( position3i );

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

				byte blockType = GetBlockData( blockHitPosition );

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
