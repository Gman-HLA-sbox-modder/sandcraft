using Sandbox;

namespace Sandblox
{
	public partial class TestEnt : ModelEntity
	{
		[Net] public int test { get; set; }
	}

	public partial class Game : Sandbox.Game
	{
		[Net] private Map Map { get; set; }

		public Game()
		{
			if ( IsServer )
			{
				_ = new HudEntity();
			}
		}

		public override void Spawn()
		{
			base.Spawn();

			Map = new Map();
			Map.SetSize( 128, 128, 64 );
			Map.GeneratePerlin();
		}

		public override void ClientSpawn()
		{
			base.ClientSpawn();

			Map.Init();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			Map?.Destroy();
		}

		public override void ClientJoined( Client client )
		{
			base.ClientJoined( client );

			var player = new Player();
			client.Pawn = player;

			player.Respawn();
		}

		public void SetBlockInDirection( Vector3 pos, Vector3 dir, byte blocktype )
		{
			var face = Map.GetBlockInDirection( pos * (1.0f / 32.0f), dir.Normal, 10000, out var hitpos, out _ );
			if ( face == Map.BlockFace.Invalid )
				return;

			var blockPos = hitpos;

			if ( blocktype != 0 )
			{
				blockPos = Map.GetAdjacentBlockPosition( blockPos, (int)face );
			}

			SetBlockOnServer( blockPos.x, blockPos.y, blockPos.z, blocktype );
		}

		public void SetBlockOnServer( int x, int y, int z, byte blocktype )
		{
			Host.AssertServer();

			if ( Map.SetBlock( new IntVector3( x, y, z ), blocktype ) )
			{
				SetBlockOnClient(x, y, z, blocktype);
			}
		}

		[ClientRpc]
		public void SetBlockOnClient( int x, int y, int z, byte blocktype )
		{
			Host.AssertClient();

			Map.SetBlockAndUpdate( new IntVector3( x, y, z ), blocktype );
		}
	}
}
