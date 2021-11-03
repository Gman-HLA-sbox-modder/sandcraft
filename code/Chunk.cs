using Sandbox;
using System;

namespace Sandblox
{
	public class Chunk
	{
		public static readonly int ChunkSize = 32;
		private static readonly int MaxFaceCount = 7000;

		private readonly Map map;
		private readonly Model model;
		private readonly Mesh mesh;
		private readonly IntVector3 offset;
		private SceneObject sceneObject;

		public Chunk( Map map, IntVector3 offset )
		{
			this.map = map;
			this.offset = offset;

			var material = Material.Load( "materials/voxel/voxel.vmat" );
			mesh = new Mesh( material );
			mesh.CreateVertexBuffer<BlockVertex>( MaxFaceCount * 6, BlockVertex.Layout );

			var boundsMin = Vector3.Zero;
			var boundsMax = boundsMin + (new Vector3( ChunkSize ) * 32);
			mesh.SetBounds( boundsMin, boundsMax );

			Rebuild();

			model = new ModelBuilder()
				.AddMesh( mesh )
				.Create();

			var transform = new Transform( new Vector3( offset.x, offset.y, offset.z ) * 32.0f );
			sceneObject = new SceneObject( model, transform );
		}

		public void Delete()
		{
			if ( sceneObject != null )
			{
				sceneObject.Delete();
				sceneObject = null;
			}
		}

		public void Rebuild()
		{
			if ( mesh.IsValid )
			{
				mesh.LockVertexBuffer<BlockVertex>( Rebuild );
			}
		}

		static readonly IntVector3[] BlockVertices = new[]
		{
			new IntVector3( 0, 0, 1 ),
			new IntVector3( 0, 1, 1 ),
			new IntVector3( 1, 1, 1 ),
			new IntVector3( 1, 0, 1 ),
			new IntVector3( 0, 0, 0 ),
			new IntVector3( 0, 1, 0 ),
			new IntVector3( 1, 1, 0 ),
			new IntVector3( 1, 0, 0 ),
		};

		static readonly int[] BlockIndices = new[]
		{
			2, 1, 0, 0, 3, 2,
			5, 6, 7, 7, 4, 5,
			5, 4, 0, 0, 1, 5,
			6, 5, 1, 1, 2, 6,
			7, 6, 2, 2, 3, 7,
			4, 7, 3, 3, 0, 4,
		};

		static readonly IntVector3[] BlockDirections = new[]
		{
			new IntVector3( 0, 0, 1 ),
			new IntVector3( 0, 0, -1 ),
			new IntVector3( -1, 0, 0 ),
			new IntVector3( 0, 1, 0 ),
			new IntVector3( 1, 0, 0 ),
			new IntVector3( 0, -1, 0 ),
		};

		static readonly int[] BlockDirectionAxis = new[]
		{
			2, 2, 0, 1, 0, 1
		};

		private static void AddQuad( Span<BlockVertex> vertices, int x, int y, int z, int width, int height, int widthAxis, int heightAxis, int face, byte blockType, int brightness )
		{
			byte textureId = (byte)(blockType - 1);
			byte normal = (byte)face;
			uint faceData = (uint)((textureId & 31) << 18 | brightness | (normal & 7) << 27);

			for ( int i = 0; i < 6; ++i )
			{
				int vi = BlockIndices[(face * 6) + i];
				var vOffset = BlockVertices[vi];

				// scale the vertex across the width and height of the face
				vOffset[widthAxis] *= width;
				vOffset[heightAxis] *= height;

				vertices[i] = new BlockVertex( (uint)(x + vOffset.x), (uint)(y + vOffset.y), (uint)(z + vOffset.z), faceData );
			}
		}

		private struct BlockFace
		{
			public bool culled;
			public byte type;
			public byte brightness;
			public byte side;

			public bool Equals( BlockFace face )
			{
				return face.culled == culled && face.type == type && face.brightness == brightness;
			}
		};

		static readonly BlockFace[] BlockFaceMask = new BlockFace[ChunkSize * ChunkSize * ChunkSize];

		BlockFace GetBlockFace( IntVector3 position, int side )
		{
			var p = offset + position;
			var blockEmpty = map.IsBlockEmpty( p.x, p.y, p.z );
			var blockIndex = blockEmpty ? 0 : map.GetBlockIndex( p.x, p.y, p.z );
			var blockType = blockEmpty ? (byte)0 : map.GetBlockData( blockIndex );
			var blockBrightness = blockEmpty ? (byte)0 : map.GetBlockBrightness( blockIndex );

			var face = new BlockFace
			{
				side = (byte)side,
				culled = blockType == 0,
				type = blockType,
				brightness = blockBrightness
			};

			if ( !face.culled && !map.IsAdjacentBlockEmpty( p.x, p.y, p.z, side ) )
			{
				var adjacentPosition = Map.GetAdjacentPos( p.x, p.y, p.z, side );
				var adjacentBlockType = map.GetBlockData( adjacentPosition.x, adjacentPosition.y, adjacentPosition.z );

				if ( adjacentBlockType != 0 )
				{
					face.culled = true;
				}
			}

			return face;
		}

		private void Rebuild( Span<BlockVertex> vertices )
		{
			int vertexOffset = 0;
			IntVector3 blockPosition;
			IntVector3 blockOffset;

			BlockFace faceA;
			BlockFace faceB;

			for ( int faceSide = 0; faceSide < 6; faceSide++ )
			{
				int axis = BlockDirectionAxis[faceSide];

				// 2 other axis
				int uAxis = (axis + 1) % 3;
				int vAxis = (axis + 2) % 3;

				blockPosition = new IntVector3( 0, 0, 0 );
				blockOffset = BlockDirections[faceSide];

				// loop through the current axis
				for ( blockPosition[axis] = 0; blockPosition[axis] < ChunkSize; blockPosition[axis]++ )
				{
					int n = 0;
					bool maskEmpty = true;

					for ( blockPosition[vAxis] = 0; blockPosition[vAxis] < ChunkSize; blockPosition[vAxis]++ )
					{
						for ( blockPosition[uAxis] = 0; blockPosition[uAxis] < ChunkSize; blockPosition[uAxis]++ )
						{
							faceB = new()
							{
								culled = true,
								side = (byte)faceSide,
								type = 0,
								brightness = 0
							};

							// face of this block
							faceA = GetBlockFace( blockPosition, faceSide );

							if ( (blockPosition[axis] + blockOffset[axis]) < ChunkSize )
							{
								// adjacent face on axis
								faceB = GetBlockFace( blockPosition + blockOffset, faceSide );
							}

							if ( !faceA.culled && !faceB.culled && faceA.Equals( faceB ) )
							{
								BlockFaceMask[n].culled = true;
							}
							else
							{
								BlockFaceMask[n] = faceA;

								if ( !faceA.culled )
								{
									// there's a face, so mask is not empty
									maskEmpty = false;
								}
							}

							n++;
						}
					}

					if ( maskEmpty )
					{
						// mask has no faces, no point going any further
						continue;
					}

					n = 0;

					for ( int j = 0; j < ChunkSize; j++ )
					{
						for ( int i = 0; i < ChunkSize; )
						{
							if ( BlockFaceMask[n].culled )
							{
								i++;
								n++;

								// if this face doesn't exist then no face is added
								continue;
							}

							int faceWidth;
							int faceHeight;

							// calculate the face width by checking if adjacent face is the same
							for ( faceWidth = 1; i + faceWidth < ChunkSize && !BlockFaceMask[n + faceWidth].culled && BlockFaceMask[n + faceWidth].Equals( BlockFaceMask[n] ); faceWidth++ ) ;

							// calculate the face height by checking if adjacent face is the same
							bool done = false;

							for ( faceHeight = 1; j + faceHeight < ChunkSize; faceHeight++ )
							{
								for ( int k = 0; k < faceWidth; k++ )
								{
									var maskFace = BlockFaceMask[n + k + faceHeight * ChunkSize];

									// face doesn't exist or there's a new type of face
									if ( maskFace.culled || !maskFace.Equals( BlockFaceMask[n] ) )
									{
										// finished, got the face height
										done = true;

										break;
									}
								}

								if ( done )
								{
									// finished, got the face height
									break;
								}
							}

							if ( !BlockFaceMask[n].culled )
							{
								blockPosition[uAxis] = i;
								blockPosition[vAxis] = j;

								var brightness = (BlockFaceMask[n].brightness & 15) << 23;

								AddQuad( vertices.Slice( vertexOffset, 6 ),
									blockPosition.x, blockPosition.y, blockPosition.z,
									faceWidth, faceHeight, uAxis, vAxis,
									BlockFaceMask[n].side, BlockFaceMask[n].type, brightness );

								vertexOffset += 6;
							}

							for ( int l = 0; l < faceHeight; ++l )
							{
								for ( int k = 0; k < faceWidth; ++k )
								{
									BlockFaceMask[n + k + l * ChunkSize].culled = true;
								}
							}

							i += faceWidth;
							n += faceWidth;
						}
					}
				}
			}

			mesh.SetVertexRange( 0, vertexOffset );
		}
	}
}
