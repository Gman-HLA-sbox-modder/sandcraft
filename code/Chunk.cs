using Sandbox;
using System;
using System.Collections.Generic;

namespace Sandblox
{
	public class Chunk
	{
		public static readonly int ChunkSize = 32;

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

			var boundsMin = Vector3.Zero;
			var boundsMax = boundsMin + (ChunkSize * 32);
			mesh.SetBounds( boundsMin, boundsMax );

			for ( int i = 0; i < Slices.Length; ++i )
			{
				Slices[i] = new Slice();
			}

			UpdateBlockSlices();
			Build();

			model = new ModelBuilder()
				.AddMesh( mesh )
				.Create();

			var transform = new Transform( offset * 32.0f );
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

		public void Build()
		{
			if ( !mesh.IsValid )
				return;

			int vertexCount = 0;
			foreach ( var slice in Slices )
			{
				vertexCount += slice.vertices.Count;
			}

			if ( mesh.HasVertexBuffer )
			{
				mesh.SetVertexBufferSize( vertexCount );
			}
			else
			{
				// If there's no verts, just put in 1 for now (temp)
				mesh.CreateVertexBuffer<BlockVertex>( Math.Max( 1, vertexCount ), BlockVertex.Layout );
			}

			vertexCount = 0;

			foreach ( var slice in Slices )
			{
				slice.dirty = false;

				if ( slice.vertices.Count == 0 )
					continue;

				mesh.SetVertexBufferData( slice.vertices, vertexCount );
				vertexCount += slice.vertices.Count;
			}

			mesh.SetVertexRange( 0, vertexCount );
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
			4, 7, 3, 3, 0, 4,
			6, 5, 1, 1, 2, 6,
			5, 4, 0, 0, 1, 5,
			7, 6, 2, 2, 3, 7,
		};

		public static readonly IntVector3[] BlockDirections = new[]
		{
			new IntVector3( 0, 0, 1 ),
			new IntVector3( 0, 0, -1 ),
			new IntVector3( 0, -1, 0 ),
			new IntVector3( 0, 1, 0 ),
			new IntVector3( -1, 0, 0 ),
			new IntVector3( 1, 0, 0 ),
		};

		static readonly int[] BlockDirectionAxis = new[]
		{
			2, 2, 1, 1, 0, 0
		};

		public static int GetOppositeDirection( int direction ) { return direction + ((direction % 2 != 0) ? -1 : 1); }

		private static void AddQuad( List<BlockVertex> vertices, int x, int y, int z, int width, int height, int widthAxis, int heightAxis, int face, byte blockType, int brightness )
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

				vertices.Add( new BlockVertex( (uint)(x + vOffset.x), (uint)(y + vOffset.y), (uint)(z + vOffset.z), faceData ) );
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

		private class Slice
		{
			public bool dirty = false;
			public List<BlockVertex> vertices = new();
		}

		static readonly BlockFace[] BlockFaceMask = new BlockFace[ChunkSize * ChunkSize * ChunkSize];

		private readonly Slice[] Slices = new Slice[ChunkSize * 6];

		BlockFace GetBlockFace( IntVector3 position, int side )
		{
			var p = offset + position;
			var blockEmpty = map.IsBlockEmpty( p );
			var blockIndex = blockEmpty ? 0 : map.GetBlockIndex( p );
			var blockType = blockEmpty ? (byte)0 : map.GetBlockData( blockIndex );
			var blockBrightness = blockEmpty ? (byte)0 : map.GetBlockBrightness( blockIndex );

			var face = new BlockFace
			{
				side = (byte)side,
				culled = blockType == 0,
				type = blockType,
				brightness = blockBrightness
			};

			if ( !face.culled && !map.IsAdjacentBlockEmpty( p, side ) )
			{
				var adjacentPosition = Map.GetAdjacentPos( p, side );
				var adjacentBlockType = map.GetBlockData( adjacentPosition );

				if ( adjacentBlockType != 0 )
				{
					face.culled = true;
				}
			}

			return face;
		}

		static int GetSliceIndex( int position, int direction )
		{
			int sliceIndex = 0;

			for ( int i = 0; i < direction; ++i )
			{
				sliceIndex += ChunkSize;
			}

			sliceIndex += position;

			return sliceIndex;
		}

		public void UpdateBlockSlice( IntVector3 position, int direction )
		{
			int vertexOffset = 0;
			int axis = BlockDirectionAxis[direction];
			int sliceIndex = GetSliceIndex( position[axis], direction );
			var slice = Slices[sliceIndex];

			if ( slice.dirty )
			{
				// already calculated this slice
				return;
			}

			slice.dirty = true;
			slice.vertices.Clear();
			BlockFace faceA;
			BlockFace faceB;

			// 2 other axis
			int uAxis = (axis + 1) % 3;
			int vAxis = (axis + 2) % 3;

			int faceSide = direction;

			var blockPosition = new IntVector3( 0, 0, 0 );
			blockPosition[axis] = position[axis];
			var blockOffset = BlockDirections[direction];

			bool maskEmpty = true;

			int n = 0;

			// loop through the 2 other axis
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
							maskEmpty = false;
						}
					}

					n++;
				}
			}

			if ( maskEmpty )
			{
				// mask has no faces, no point going any further
				return;
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

						AddQuad( slice.vertices,
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

		private void UpdateBlockSlices()
		{
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

					int sliceIndex = GetSliceIndex( blockPosition[axis], faceSide );
					var slice = Slices[sliceIndex];
					slice.dirty = true;
					slice.vertices.Clear();

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

								AddQuad( slice.vertices,
									blockPosition.x, blockPosition.y, blockPosition.z,
									faceWidth, faceHeight, uAxis, vAxis,
									BlockFaceMask[n].side, BlockFaceMask[n].type, brightness );
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
		}
	}
}
