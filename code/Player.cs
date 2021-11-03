using Sandbox;

namespace Sandblox
{
	partial class Player : Sandbox.Player
	{
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

		public override void BuildInput( InputBuilder input )
		{
			base.BuildInput( input );

			if ( (input.Down( InputButton.Duck ) ? input.Down( InputButton.Attack1 ) : input.Pressed( InputButton.Attack1 )) )
			{
				(Sandbox.Game.Current as Game).SetBlock( EyePos, EyeRot.Forward, (byte)Rand.Int( 1, 5 ) );
			}
			else if ( (input.Down( InputButton.Duck ) ? input.Down( InputButton.Attack2 ) : input.Pressed( InputButton.Attack2 )) )
			{
				//(Sandbox.Game.Current as Game).SetBlock( EyePos, EyeRot.Forward, 0 );
			}
		}
	}
}
