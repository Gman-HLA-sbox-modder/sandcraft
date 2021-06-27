using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Sandblox
{
	public partial class Player : Sandbox.Player
	{
		[Net, Local, OnChangedCallback] public List<int> LoadedChunks { get; set; }
		private float NextUpdateRenderRange { get; set; }

		public List<ServerChunkStream> OutgoingChunks { get; } = new();

		public override void Respawn()
		{
			Controller = new NoclipController();
			Camera = new FirstPersonCamera();

			EnableAllCollisions = true;
			EnableDrawing = true;
			EnableHideInFirstPerson = true;
			EnableShadowInFirstPerson = true;

			base.Respawn();
		}

		public void SendChunk( int x, int y, int z )
		{
			var chunkSize = Chunk.ChunkSize;
			var numChunksX = Game.map.SizeX / chunkSize;
			var numChunksY = Game.map.SizeY / chunkSize;
			var chunkIndex = x + y * numChunksX + z * numChunksX * numChunksY;

			if ( LoadedChunks.Contains( chunkIndex ) ) return;

			var data = Game.map.GetChunkData( x, y, z );

			// You probably wanna stream these.
			Game.StreamChunkTo( this, x, y, z, data );

			LoadedChunks.Add( chunkIndex );
		}

		public override void Simulate( Client cl )
		{
			base.Simulate( cl );

			if ( IsServer && (Input.Down( InputButton.Duck ) ? Input.Down( InputButton.Attack1 ) : Input.Pressed( InputButton.Attack1 )) )
			{
				(Sandbox.Game.Current as Game).SetBlock( EyePos, EyeRot.Forward, (byte)Rand.Int( 1, 5 ) );
			}
			else if ( IsServer && (Input.Down( InputButton.Duck ) ? Input.Down( InputButton.Attack2 ) : Input.Pressed( InputButton.Attack2 )) )
			{
				(Sandbox.Game.Current as Game).SetBlock( EyePos, EyeRot.Forward, 0 );
			}
		}

		[Event.Tick.Server]
		private void UpdateRenderRange()
		{
			if ( Time.Now < NextUpdateRenderRange ) return;

			// How many chunks can we see around us?
			var maximumRange = 16;
			var renderRange = 10;
			var chunkSize = Chunk.ChunkSize;
			var mapSizeZ = Game.map.SizeZ;
			var numChunksZ = mapSizeZ / chunkSize;
			var chunksInRange = new HashSet<int>();

			for ( var x = -maximumRange; x < maximumRange; x++ )
			{
				for ( var y = -maximumRange; y < maximumRange; y++ )
				{
					for ( var z = 0; z <= numChunksZ; z++ )
					{
						var chunkWorldPosition = Position.WithZ( z * chunkSize * 32f ) + new Vector3( x * chunkSize * 32f, y * chunkSize * 32f );

						if ( !Game.map.IsValidChunkAt( chunkWorldPosition ) ) continue;

						var chunkIndex = Game.map.ToChunkIndex( chunkWorldPosition );

						// Check whether we should have this chunk loaded and send it if so.
						if ( x <= renderRange && y <= renderRange && !LoadedChunks.Contains( chunkIndex ) )
						{
							var chunkPosition = Game.map.ToChunkPosition( chunkWorldPosition );
							SendChunk( chunkPosition.x, chunkPosition.y, chunkPosition.z );
						}

						// It's still in range (we don't tell the client to hide it).
						chunksInRange.Add( chunkIndex );
					}
				}
			}

			// Cache it because it's a property.
			var loadedChunks = LoadedChunks;

			for ( var i = loadedChunks.Count - 1; i >= 0; i--)
			{
				var loadedIndex = loadedChunks[i];

				if ( !chunksInRange.Contains( loadedIndex ) )
				{
					RemoveOutgoingChunk( loadedIndex );
					loadedChunks.RemoveAt( i );
				}
			}

			NextUpdateRenderRange = Time.Now + 1f;
		}

		private void RemoveOutgoingChunk( int index )
		{
			for ( var i = 0; i < OutgoingChunks.Count; i++ )
			{
				var stream = OutgoingChunks[i];

				if ( stream.index == index )
				{
					OutgoingChunks.RemoveAt( i );
				}
			}
		}

		private void OnLoadedChunksChanged()
		{
			var loadedChunksAsHashSet = new HashSet<int>( LoadedChunks );
			var chunks = Game.chunks;

			if ( chunks == null )
			{
				// We aren't loaded in fully yet.
				return;
			}

			for ( var i = 0; i < chunks.Length; i++ )
			{
				var chunk = chunks[i];

				if ( !loadedChunksAsHashSet.Contains( i ) && chunk != null )
				{
					// We can't see this chunk anymore!
					chunk.Disable();
				}
			}
		}

		[Event.Tick.Server]
		private void TickChunkStream()
		{
			if ( OutgoingChunks.Count == 0 ) return;

			var stream = OutgoingChunks[0];
			var streamLen = stream.end - stream.start;
			var data = new byte[streamLen];

			var dataIndex = 0;
			for ( var i = stream.start; i < stream.end; i++ )
			{
				data[dataIndex] = stream.data[i];
				dataIndex++;
			}

			Game.ClientReceiveChunkData( stream.index, stream.start, stream.end, data );

			OutgoingChunks.RemoveAt( 0 );
		}
	}
}
