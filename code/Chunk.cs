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
			if ( mesh.IsValid )
			{
				mesh.DeleteBuffers();
			}

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

		private void Rebuild( Span<BlockVertex> vertices )
		{
			int vertexOffset = 0;

			for ( int x = 0; x < ChunkSize; ++x )
			{
				for ( int y = 0; y < ChunkSize; ++y )
				{
					for ( int z = 0; z < ChunkSize; ++z )
					{
						var mx = offset.x + x;
						var my = offset.y + y;
						var mz = offset.z + z;

						var blockIndex = map.GetBlockIndex( mx, my, mz );
						var blockType = map.GetBlockData( blockIndex );

						if ( blockType != 0 )
						{
							var brightness = (int)map.GetBlockBrightness( blockIndex );
							brightness = (brightness & 15) << 23;

							for ( int face = 0; face < 6; ++face )
							{
								if ( !map.IsAdjacentBlockEmpty( mx, my, mz, face ) )
									continue;

								if ( vertexOffset + 6 >= vertices.Length )
									break;

								AddQuad( vertices.Slice( vertexOffset, 6 ), x, y, z, face, blockType, brightness );
								vertexOffset += 6;
							}
						}
					}
				}
			}

			mesh.SetVertexRange( 0, vertexOffset );
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

		private static void AddQuad( Span<BlockVertex> vertices, int x, int y, int z, int face, byte blockType, int brightness )
		{
			byte textureId = (byte)(blockType - 1);
			byte normal = (byte)face;
			uint faceData = (uint)((textureId & 31) << 18 | brightness | (normal & 7) << 27);

			for ( int i = 0; i < 6; ++i )
			{
				int vi = BlockIndices[(face * 6) + i];
				var vOffset = BlockVertices[vi];
				vertices[i] = new BlockVertex( (uint)(x + vOffset.x), (uint)(y + vOffset.y), (uint)(z + vOffset.z), faceData );
			}
		}
	}
}
