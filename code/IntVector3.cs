using System;

namespace Sandblox
{
	public struct IntVector3
	{
		public int x;
		public int y;
		public int z;

		public IntVector3( int x, int y, int z )
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static IntVector3 operator +( IntVector3 c1, IntVector3 c2 )
		{
			return new IntVector3( c1.x + c2.x, c1.y + c2.y, c1.z + c2.z );
		}

		public int this[int index]
		{
			get
			{
				return index switch
				{
					0 => x,
					1 => y,
					2 => z,
					_ => throw new IndexOutOfRangeException(),
				};
			}

			set
			{
				switch ( index )
				{
					case 0: x = value; break;
					case 1: y = value; break;
					case 2: z = value; break;
				}
			}
		}
	}
}
