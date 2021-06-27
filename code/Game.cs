using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandblox
{
	public struct ClientChunkStream
	{
		public int x;
		public int y;
		public int z;
		public int received;
		public int length;
		public byte[] data;
	}

	public struct ServerChunkStream
	{
		public int x;
		public int y;
		public int z;
		public int index;
		public byte[] data;
		public int start;
		public int end;
	}

	[Library( "sandblox" )]
	public partial class Game : Sandbox.Game
	{
		public static Map map;
		public static Chunk[] chunks;
		public static Dictionary<int, ClientChunkStream> incomingChunks = new();

		public static void StreamChunkTo( Player player, int x, int y, int z, byte[] data )
		{
			var dataLen = data.Length;

			ClientStartStream( To.Single( player ), x, y, z, data.Length );

			var chunkSize = Chunk.ChunkSize;
			var numChunksX = map.SizeX / chunkSize;
			var numChunksY = map.SizeY / chunkSize;
			var chunkIndex = x + y * numChunksX + z * numChunksX * numChunksY;

			var startIndex = 0;
			var streamSize = dataLen / 2;

			while ( startIndex < dataLen )
			{
				var endIndex = Math.Min( startIndex + streamSize, dataLen );
				var outgoing = new ServerChunkStream()
				{
					x = x,
					y = y,
					z = z,
					index = chunkIndex,
					data = data,
					start = startIndex,
					end = endIndex
				};

				startIndex += streamSize;

				player.OutgoingChunks.Add( outgoing );
			}
		}

		[ClientRpc]
		public static void ClientStartStream( int x, int y, int z, int length )
		{
			var stream = new ClientChunkStream()
			{
				x = x,
				y = y,
				z = z,
				length = length,
				received = 0,
				data = new byte[length]
			};

			var chunkSize = Chunk.ChunkSize;
			var numChunksX = map.SizeX / chunkSize;
			var numChunksY = map.SizeY / chunkSize;
			var chunkIndex = x + y * numChunksX + z * numChunksX * numChunksY;

			incomingChunks[chunkIndex] = stream;
		}
		
		[ClientRpc]
		public static void ClientReceiveChunkData( int index, int start, int end, byte[] data )
		{
			if ( incomingChunks.TryGetValue( index, out var stream ) )
			{
				var dataIndex = 0;

				for ( var i = start; i < end; i++ )
				{
					stream.data[i] = data[dataIndex];
					dataIndex++;
				}

				stream.received += data.Length;

				if ( stream.received == stream.length )
				{
					// We have the whole thing.
					incomingChunks.Remove( index );
					ClientReceiveChunk( stream.x, stream.y, stream.z, stream.data );
				}
				else
				{
					incomingChunks[index] = stream;
				}
			}
		}

		[ClientRpc]
		public static void ClientInitializeMap( int seed, int sizeX, int sizeY, int sizeZ )
		{
			map = new Map( seed, sizeX, sizeY, sizeZ );

			var numChunksX = map.SizeX / Chunk.ChunkSize;
			var numChunksY = map.SizeY / Chunk.ChunkSize;
			var numChunksZ = map.SizeZ / Chunk.ChunkSize;

			chunks = new Chunk[(numChunksX * numChunksY * numChunksZ)];
		}

		[ClientRpc]
		public static void ClientReceiveChunk( int x, int y, int z, byte[] data )
		{
			var numChunksX = map.SizeX / Chunk.ChunkSize;
			var numChunksY = map.SizeY / Chunk.ChunkSize;
			var chunkIndex = x + y * numChunksX + z * numChunksX * numChunksY;
			var chunkSize = Chunk.ChunkSize;
			var chunkOffset = new IntVector3( x * chunkSize, y * chunkSize, z * chunkSize );

			for ( var lx = 0; lx < chunkSize; lx++ )
			{
				for ( var ly = 0; ly < chunkSize; ly++ )
				{
					for ( var lz = 0; lz < chunkSize; lz++ )
					{
						var localBlockIndex = (lx + ly * chunkSize + lz * chunkSize * chunkSize) * 2;
						var localBlockType = data[localBlockIndex];
						var localBlockHealth = data[localBlockIndex + 1];
						var offset = new IntVector3( x * chunkSize, y * chunkSize, z * chunkSize );
						var mx = offset.x + lx;
						var my = offset.y + ly;
						var mz = offset.z + lz;

						map.SetBlock( mx, my, mz, localBlockType, localBlockHealth );
					}
				}
			}

			if ( chunks[chunkIndex] != null )
			{
				chunks[chunkIndex].Enable();
				chunks[chunkIndex].Rebuild();
			}
			else
			{
				chunks[chunkIndex] = new Chunk( map, chunkOffset );
				chunks[chunkIndex].Rebuild();
			}
		}

		[ClientRpc]
		public static void ClientUpdateBlock( int x, int y, int z, byte blocktype )
		{
			var chunkSize = Chunk.ChunkSize;
			var numChunksX = map.SizeX / chunkSize;
			var numChunksY = map.SizeY / chunkSize;

			if ( map.SetBlock( x, y, z, blocktype ) )
			{
				var chunkIndex = (x / chunkSize) + (y / chunkSize) * numChunksX + (z / chunkSize) * numChunksX * numChunksY;

				if ( chunks[chunkIndex] != null )
					chunks[chunkIndex].Rebuild();
			}
		}

		public Game()
		{
			if ( IsServer )
			{
				_ = new HudEntity();

				map = new Map( 1337, 512, 512, 128 );
				map.GeneratePerlin();
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if ( chunks != null )
			{
				foreach ( var chunk in chunks )
				{
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

			ClientInitializeMap( To.Single( client ), map.Seed, map.SizeX, map.SizeY, map.SizeZ );

			player.SendChunk( 0, 0, 0 );
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
			var players = All.OfType<Player>();

			var numChunksX = map.SizeX / Chunk.ChunkSize;
			var numChunksY = map.SizeY / Chunk.ChunkSize;

			for ( int x2 = 0; x2 < 1; ++x2 )
			{
				for ( int y2 = 0; y2 < 1; ++y2 )
				{
					for ( int z2 = 0; z2 < 1; ++z2 )
					{
						var x3 = x + x2;
						var y3 = y + y2;
						var z3 = z + z2;

						if ( map.SetBlock( x3, y3, z3, blocktype ) )
						{
							var chunkIndex = (x3 / Chunk.ChunkSize) + (y3 / Chunk.ChunkSize) * numChunksX + (z3 / Chunk.ChunkSize) * numChunksX * numChunksY;

							// Otherwise we won't receive the RPC
							using ( Prediction.Off() )
							{
								foreach ( var p in players )
								{
									if ( p.LoadedChunks.Contains( chunkIndex ) )
									{
										ClientUpdateBlock( To.Single( p ), x3, y3, z3, blocktype );
									}
								}
							}

							build = true;
						}
					}
				}
			}

			return build;
		}
	}
}
