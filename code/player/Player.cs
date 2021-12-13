using Sandbox;
using Sandbox.UI;

namespace Sandblox
{
	partial class Player : Sandbox.Player
	{
		public override void Respawn()
		{
			SetModel( "models/citizen/citizen.vmdl" );

			Controller = new WalkController();
			Animator = new StandardPlayerAnimator();
			Camera = new FirstPersonCamera();

			EnableAllCollisions = true;
			EnableDrawing = true;
			EnableHideInFirstPerson = true;
			EnableShadowInFirstPerson = true;

			base.Respawn();
		}

		public override void OnKilled()
		{
			base.OnKilled();

			Controller = null;
			Animator = null;
			Camera = null;

			EnableAllCollisions = false;
			EnableDrawing = false;
		}

		public override void Simulate( Client cl )
		{
			base.Simulate( cl );

			if ( !IsServer )
				return;

			if ( Input.Pressed( InputButton.Attack1 ) )
			{
				(Sandbox.Game.Current as Game).SetBlockInDirection( Input.Position, Input.Rotation.Forward, (byte)Rand.Int( 1, 5 ) );
			}
			else if ( Input.Pressed( InputButton.Attack2 ) )
			{
				(Sandbox.Game.Current as Game).SetBlockInDirection( Input.Position, Input.Rotation.Forward, 0 );
			}
		}
	}
}
