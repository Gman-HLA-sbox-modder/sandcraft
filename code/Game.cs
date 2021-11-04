﻿using Sandbox;
using System.Collections.Generic;

namespace Sandblox
{
	[Library( "sandblox" )]
	[Hammer.Skip]
	public partial class Game : Sandbox.Game
	{
		private readonly Map map;
		private readonly Chunk[] chunks;

		public Game()
		{
			if ( IsServer )
			{
				_ = new HudEntity();
			}

			map = new Map( 256, 256, 64 );
			map.GeneratePerlin();

			if ( IsClient )
			{
				var numChunksX = map.SizeX / Chunk.ChunkSize;
				var numChunksY = map.SizeY / Chunk.ChunkSize;
				var numChunksZ = map.SizeZ / Chunk.ChunkSize;

				chunks = new Chunk[(numChunksX * numChunksY * numChunksZ)];

				for ( int x = 0; x < numChunksX; ++x )
				{
					for ( int y = 0; y < numChunksY; ++y )
					{
						for ( int z = 0; z < numChunksZ; ++z )
						{
							var chunkIndex = x + y * numChunksX + z * numChunksX * numChunksY;
							var chunk = new Chunk( map, new IntVector3( x * Chunk.ChunkSize, y * Chunk.ChunkSize, z * Chunk.ChunkSize ) );
							chunks[chunkIndex] = chunk;
						}
					}
				}
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

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

		public override void ClientJoined( Client client )
		{
			base.ClientJoined( client );

			var player = new Player();
			client.Pawn = player;

			player.Respawn();
		}

		public bool SetBlock( Vector3 pos, Vector3 dir, byte blocktype )
		{
			var f = map.GetBlockInDirection( pos * (1.0f / 32.0f), dir.Normal, 10000, out var hitpos, out _ );
			if ( f == Map.BlockFace.Invalid )
				return false;

			var x = hitpos.x;
			var y = hitpos.y;
			var z = hitpos.z;

			if ( blocktype != 0 )
			{
				var b = Map.GetAdjacentPos( x, y, z, (int)f );
				x = b.x;
				y = b.y;
				z = b.z;
			}

			bool build = false;
			var chunkids = new HashSet<int>();

			var numChunksX = map.SizeX / Chunk.ChunkSize;
			var numChunksY = map.SizeY / Chunk.ChunkSize;

			var x3 = x;
			var y3 = y;
			var z3 = z;

			if ( map.SetBlock( x3, y3, z3, blocktype ) )
			{
				var chunkIndex = (x3 / Chunk.ChunkSize) + (y3 / Chunk.ChunkSize) * numChunksX + (z3 / Chunk.ChunkSize) * numChunksX * numChunksY;

				chunkids.Add( chunkIndex );

				build = true;

				for ( int i = 0; i < 6; i++ )
				{
					if ( map.IsAdjacentBlockEmpty( x3, y3, z3, i ) )
					{
						var posInChunk = new IntVector3( x3 % Chunk.ChunkSize, y3 % Chunk.ChunkSize, z3 % Chunk.ChunkSize );
						chunks[chunkIndex].UpdateBlockSlice( posInChunk, i );
						continue;
					}

					var adjacentPos = Map.GetAdjacentPos( x3, y3, z3, i );
					var adjadentChunkIndex = (adjacentPos.x / Chunk.ChunkSize) + (adjacentPos.y / Chunk.ChunkSize) * numChunksX + (adjacentPos.z / Chunk.ChunkSize) * numChunksX * numChunksY;
					var adjacentPosInChunk = new IntVector3( adjacentPos.x % Chunk.ChunkSize, adjacentPos.y % Chunk.ChunkSize, adjacentPos.z % Chunk.ChunkSize );

					chunkids.Add( adjadentChunkIndex );

					chunks[adjadentChunkIndex].UpdateBlockSlice( adjacentPosInChunk, Chunk.GetOppositeDirection( i ) );
				}
			}

			foreach ( var chunkid in chunkids )
			{
				chunks[chunkid].Build();
			}

			return build;
		}
	}
}
