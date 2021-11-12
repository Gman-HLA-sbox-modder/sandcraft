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

		public override void Simulate( Client cl )
		{
			base.Simulate( cl );

			if ( !IsServer )
				return;

			if ( (Input.Down( InputButton.Duck ) ? Input.Down( InputButton.Attack1 ) : Input.Pressed( InputButton.Attack1 )) )
			{
				(Sandbox.Game.Current as Game).SetBlockInDirection( EyePos, EyeRot.Forward, (byte)Rand.Int( 1, 5 ) );
			}
			else if ( (Input.Down( InputButton.Duck ) ? Input.Down( InputButton.Attack2 ) : Input.Pressed( InputButton.Attack2 )) )
			{
				(Sandbox.Game.Current as Game).SetBlockInDirection( EyePos, EyeRot.Forward, 0 );
			}

			if ( Input.Down( InputButton.Jump ) )
			{
				var r = Input.Rotation;
				var ent = new Prop
				{
					Position = Input.Position + r.Forward * 50,
					Rotation = r
				};

				ent.SetModel( "models/citizen_props/crate01.vmdl" );
				ent.Velocity = r.Forward * 4000;
			}
		}
	}
}
