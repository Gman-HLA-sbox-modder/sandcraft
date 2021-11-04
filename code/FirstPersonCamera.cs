using Sandbox;

namespace Sandblox
{
	public class FirstPersonCamera : Camera
	{
		public override void Activated()
		{
			var pawn = Local.Pawn;
			if ( pawn == null ) return;

			Position = pawn.EyePos;
			Rotation = pawn.EyeRot;
		}

		public override void Update()
		{
			var pawn = Local.Pawn;
			if ( pawn == null ) return;

			Position = pawn.EyePos;
			Rotation = pawn.EyeRot;

			FieldOfView = 90;

			Viewer = pawn;
		}
	}
}
