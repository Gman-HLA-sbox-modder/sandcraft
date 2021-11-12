using Sandbox;

namespace Sandblox
{
	public partial class Game : Sandbox.Game
	{
		private Map Map { get; set; }

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
		}

		public override void ClientSpawn()
		{
			base.ClientSpawn();

			Map = new Map( 256, 256, 64 );
			Map.GeneratePerlin();
			Map.Init();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			Map.Destroy();
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
			return Map.SetBlock( pos, dir, blocktype );
		}
	}
}
