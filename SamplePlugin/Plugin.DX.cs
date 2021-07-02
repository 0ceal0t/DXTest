using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using Vec2 = System.Numerics.Vector2;
using SharpDX.Direct3D;

using Dalamud.Hooking;
using Dalamud.Plugin;
using System.Runtime.InteropServices;

namespace SamplePlugin {
    public partial class Plugin {
        private delegate void RenderDelegate(IntPtr a1, int a2, IntPtr a3, byte a4, IntPtr a5, IntPtr a6);
        private Hook<RenderDelegate> RenderHook;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr GetMatrixSingletonDelegate();
        internal GetMatrixSingletonDelegate GetMatrixSingleton;

        Device Device;
        DeviceContext Ctx;
        string ShaderPath;

        int NumVerts;
        Buffer Vertices;
        CompilationResult VertexShaderByteCode;
        CompilationResult PixelShaderByteCode;
        PixelShader PShader;
        VertexShader VShader;
        ShaderSignature Signature;
        InputLayout Layout;

        Buffer WorldBuffer;

        /*
         * auto renderPtr = game::MakeBaseRelativePtr<void>(0x368AF0);
            MH_CreateHook(renderPtr, &Hook_RenderManager_Render, (void**)&Orig_RenderManager_Render);
            MH_EnableHook(renderPtr);

            its 3A0730 is PostEffectManager::Render

            posteffectmanager::render is called by 369240 which is called in the render function so idk whats going on here

            0x1404B30D0 - UI render
        48 89 5C 24 ? 48 89 6C 24 ? 56 57 41 54 41 56 41 57 48 83 EC 40 44 8B 05 ? ? ? ?

        E8 ?? ?? ?? ?? 48 81 C3 ?? ?? ?? ?? BE ?? ?? ?? ?? 45 33 F6
            private delegate byte RenderDelegate(IntPtr a1, IntPtr a2);

        85 D2 0F 84 ?? ?? ?? ?? 41 54
            
         */

        public void InitDX() {
            var matrixAddr = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? 48 89 4c 24 ?? 4C 8D 4D ?? 4C 8D 44 24 ??");
            GetMatrixSingleton = Marshal.GetDelegateForFunctionPointer<GetMatrixSingletonDelegate>(matrixAddr);

            IntPtr renderPtr = PluginInterface.TargetModuleScanner.ScanText("41 54 41 56 41 57 48 81 EC ?? ?? ?? ?? 44 8B 41 18 44 8B FA 48 8B 05 ?? ?? ?? ?? 4C 8B F1 4C 89 AC 24 ?? ?? ?? ?? 41 0F B6 D1"); // changes recast partId for gcds
            RenderHook = new Hook<RenderDelegate>(renderPtr, (RenderDelegate)OnRender);
            RenderHook.Enable();

            ShaderPath = Path.Combine(Path.GetDirectoryName(AssemblyLocation), "Shaders");
            Device = PluginInterface.UiBuilder.Device;
            Ctx = Device.ImmediateContext;

            WorldBuffer = new Buffer(Device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            var shaderFile = Path.Combine(ShaderPath, "Test.fx");
            VertexShaderByteCode = ShaderBytecode.CompileFromFile(shaderFile, "VS", "vs_4_0");
            PixelShaderByteCode = ShaderBytecode.CompileFromFile(shaderFile, "PS", "ps_4_0");
            VShader = new VertexShader(Device, VertexShaderByteCode);
            PShader = new PixelShader(Device, PixelShaderByteCode);
            Signature = ShaderSignature.GetInputSignature(VertexShaderByteCode);
            Layout = new InputLayout(Device, Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0)
            });

            var baseVertices = new[]
            {
                new Vector4(0.0f, 0.0f, 10.0f, 1.0f),
                new Vector4(100.0f, 0.0f, 10.0f, 1.0f),
                new Vector4(100.0f, 100.0f, 10.0f, 1.0f)
            };
            NumVerts = 3;
            Vertices = Buffer.Create(Device, BindFlags.VertexBuffer, baseVertices);
        }

        public void OnRender(IntPtr a1, int a2, IntPtr a3, byte a4, IntPtr a5, IntPtr a6) {
            Render();
            RenderHook.Original(a1, a2, a3, a4, a5, a6);
        }

        public void Render() {
            var matrixSingleton = GetMatrixSingleton();

            var viewProjectionMatrix = default(Matrix);
            float width, height;
            unsafe {
                var rawMatrix = (float*)(matrixSingleton + 0x1b4).ToPointer();
                for (var i = 0; i < 16; i++, rawMatrix++) {
                    viewProjectionMatrix[i] = *rawMatrix;
                }
                width = *rawMatrix;
                height = *(rawMatrix + 1);
            }

            Device.ImmediateContext.UpdateSubresource(ref viewProjectionMatrix, WorldBuffer);

            Ctx.PixelShader.Set(PShader);
            Ctx.VertexShader.Set(VShader);
            Ctx.InputAssembler.InputLayout = Layout;
            Ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            Ctx.VertexShader.SetConstantBuffer(0, WorldBuffer);
            Ctx.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(Vertices, Utilities.SizeOf<Vector4>() * 1, 0));
            Ctx.Draw(NumVerts, 0);
            Ctx.Flush();
        }

        public void DisposeDX() {
            RenderHook.Disable();
            RenderHook.Dispose();

            Vertices?.Dispose();
            Layout?.Dispose();
            Signature?.Dispose();
            PShader?.Dispose();
            VShader?.Dispose();
            VertexShaderByteCode?.Dispose();
            PixelShaderByteCode?.Dispose();

            WorldBuffer?.Dispose();

            Device = null;
            Ctx = null;
        }
    }
}
