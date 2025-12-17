using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/*
 * Blit Renderer Feature                                                https://github.com/Cyanilux/URP_BlitRenderFeature
 * ------------------------------------------------------------------------------------------------------------------------
 * Based on the Blit from the UniversalRenderingExamples
 * https://github.com/Unity-Technologies/UniversalRenderingExamples/tree/master/Assets/Scripts/Runtime/RenderPasses
 * 
 * Extended to allow for :
 * - Specific access to selecting a source and destination (via current camera's color / texture id / render texture object
 * - (Pre-2021.2/v12) Automatic switching to using _AfterPostProcessTexture for After Rendering event, in order to correctly handle the blit after post processing is applied
 * - Setting a _InverseView matrix (cameraToWorldMatrix), for shaders that might need it to handle calculations from screen space to world.
 * 		e.g. Reconstruct world pos from depth : https://www.cyanilux.com/tutorials/depth/#blit-perspective 
 * - (2020.2/v10 +) Enabling generation of DepthNormals (_CameraNormalsTexture)
 * 		This will only include shaders who have a DepthNormals pass (mostly Lit Shaders / Graphs)
 		(workaround for Unlit Shaders / Graphs: https://gist.github.com/Cyanilux/be5a796cf6ddb20f20a586b94be93f2b)
 * ------------------------------------------------------------------------------------------------------------------------
 * @Cyanilux
*/

namespace Cyan {
/*
CreateAssetMenu here allows creating the ScriptableObject without being attached to a Renderer Asset
Can then Enqueue the pass manually via https://gist.github.com/Cyanilux/8fb3353529887e4184159841b8cad208
as a workaround for 2D Renderer not supporting features (prior to 2021.2). Uncomment if needed.
*/
//	[CreateAssetMenu(menuName = "Cyan/Blit")] 
	public class Blit : ScriptableRendererFeature {

		// Render Graph pass data
		private class PassData {
			public Material blitMaterial;
			public int blitMaterialPassIndex;
			public BlitSettings settings;
			public TextureHandle source;
			public TextureHandle destination;
		}

		public class BlitRenderGraphScriptablePass : ScriptableRenderPass {
			private BlitSettings m_Settings;
			private Material m_BlitMaterial;
			private string m_PassName;

			public BlitRenderGraphScriptablePass(BlitSettings settings, Material blitMaterial, string passName) {
				m_Settings = settings;
				m_BlitMaterial = blitMaterial;
				m_PassName = passName;
				renderPassEvent = settings.Event;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
				UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
				UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

				// Get source texture
				TextureHandle sourceTexture;
				if (m_Settings.srcType == Target.CameraColor) {
					sourceTexture = resourceData.activeColorTexture;
				} else if (m_Settings.srcType == Target.TextureID) {
					sourceTexture = renderGraph.ImportTexture(RTHandles.Alloc(m_Settings.srcTextureId));
				} else {
					sourceTexture = renderGraph.ImportTexture(RTHandles.Alloc(m_Settings.srcTextureObject));
				}

				// Create or get destination texture
				TextureHandle destinationTexture;
				if (m_Settings.dstType == Target.CameraColor) {
					destinationTexture = resourceData.activeColorTexture;
				} else {
					var desc = cameraData.cameraTargetDescriptor;
					desc.depthBufferBits = 0;
					if (m_Settings.overrideGraphicsFormat) {
						desc.graphicsFormat = m_Settings.graphicsFormat;
					}
					
					destinationTexture = UniversalRenderer.CreateRenderGraphTexture(
						renderGraph, desc, m_Settings.dstTextureId, false, FilterMode.Point);
				}

				// Add render pass
				using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_PassName, out var passData)) {
					passData.blitMaterial = m_BlitMaterial;
					passData.blitMaterialPassIndex = m_Settings.blitMaterialPassIndex;
					passData.settings = m_Settings;
					
					// Store texture handles for use in render function
					passData.source = sourceTexture;
					passData.destination = destinationTexture;
					
					// Declare texture usage for render graph dependency tracking
					builder.UseTexture(sourceTexture, AccessFlags.Read);
					builder.SetRenderAttachment(destinationTexture, 0, AccessFlags.Write);

					builder.SetRenderFunc<PassData>((PassData data, RasterGraphContext context) => {
						if (data.settings.setInverseViewMatrix) {
							Shader.SetGlobalMatrix("_InverseView", cameraData.camera.cameraToWorldMatrix);
						}

						Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 
							data.blitMaterial, data.blitMaterialPassIndex);
					});
				}
			}

			// Legacy method for compatibility mode (won't be used with render graph enabled)
			[System.Obsolete]
			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
				// This method is not used when render graph is enabled
			}
		}

		[System.Serializable]
		public class BlitSettings {
			public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;

			public Material blitMaterial = null;
			public int blitMaterialPassIndex = 0;
			public bool setInverseViewMatrix = false;
			public bool requireDepthNormals = false;

			public Target srcType = Target.CameraColor;
			public string srcTextureId = "_CameraColorTexture";
			public RenderTexture srcTextureObject;

			public Target dstType = Target.CameraColor;
			public string dstTextureId = "_BlitPassTexture";
			public RenderTexture dstTextureObject;

			public bool overrideGraphicsFormat = false;
			public UnityEngine.Experimental.Rendering.GraphicsFormat graphicsFormat;
		}

		public enum Target {
			CameraColor,
			TextureID,
			RenderTextureObject
		}

		public BlitSettings settings = new BlitSettings();

		public override void Create() {
			var passIndex = settings.blitMaterial != null ? settings.blitMaterial.passCount - 1 : 1;
			settings.blitMaterialPassIndex = Mathf.Clamp(settings.blitMaterialPassIndex, -1, passIndex);

			if (settings.graphicsFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.None) {
				settings.graphicsFormat = SystemInfo.GetGraphicsFormat(UnityEngine.Experimental.Rendering.DefaultFormat.LDR);
			}
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
			if (settings.blitMaterial == null) {
				Debug.LogWarningFormat("Missing Blit Material. {0} blit pass will not execute. Check for missing reference in the assigned renderer.", GetType().Name);
				return;
			}

			// In Unity 6 with Render Graph, we create a render graph pass and enqueue it
			var pass = new BlitRenderGraphScriptablePass(settings, settings.blitMaterial, name);
			renderer.EnqueuePass(pass);
		}
	}
}