using Sandbox;

namespace Sandblox
{
	public partial class Game : Sandbox.Game
	{
		private readonly Map map;

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
				map.Init();
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			map.Destroy();
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
			return map.SetBlock( pos, dir, blocktype );
		}
	}
}
