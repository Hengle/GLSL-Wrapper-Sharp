using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.IO;

namespace ShaderCompiler
{
	using StageItem = Tuple<string, Program.ShaderStage, string>;
	using UniformItem = Tuple<ActiveUniformType, string>;

	public static class Extensions
	{
		public static string[] SplitWhere(this string arg, Predicate<char> pred)
		{
			List<string> strs = new List<string>();
			int prev = 0;
			int i = 0;

			for (i = 0; i < arg.Length; i++)
			{
				if (pred(arg[i]))
				{
					strs.Add(arg.Substring(prev, i - prev));
					prev = i;
				}
			}

			if (prev != i)
			{
				strs.Add(arg.Substring(i));
			}

			return strs.ToArray();
		}
		public static string TrimMatchingQuotes(this string arg)
		{
			if (arg[0] == '\"' && arg.Last() == '\"')
			{
				return arg.Substring(1, arg.Length - 2);
			}
			return arg;
		}
	}

	[Serializable]
	public class GLVersionException : Exception
	{
		public Version Version;
		public Version Expected;

		public GLVersionException(Version v, Version e) { this.Version = v; this.Expected = e; }
		public GLVersionException(string message) : base(message) { }
		public GLVersionException(string message, Exception inner) : base(message, inner) { }
		protected GLVersionException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

	class Program
	{
		static readonly Version CompilerVersion = new Version(0, 1, 10);

		static GameWindow Window;
		ArgInfo Info;
		List<Tuple<ActiveUniformType, string>> Uniforms = new List<Tuple<ActiveUniformType, string>>();
		List<Tuple<ActiveAttribType, string>> Attributes = new List<Tuple<ActiveAttribType, string>>();

		/*
		 * Arguments: (May be prefixed with - or /)
		 *	- r : RecompileFromFile (Reloads the shader from files in the current directory whenever the shader is compiled)
		 *	- s : Embeds the shader as strings in the output file (this is the default)
		 *	- out=[filename] : Sets the output file (The default is __Shader[random identifier(Only changes if the arguments change)])
		 *	- name=[string] : Sets the name of the shader (This will be the name of the output class)
		 *	- [filename] : One of the files to compile as a shader stage (The stage of the shader is inferred from the extension)
		 *	- vert=[filename] : Compiles the file as a vertex shader
		 *	- frag=[filename] : Compiles the file as a fragment shader
		 *	- geom=[filename] : Compiles the file as a geometry shader
		 *	- tessEval=[filename] : Compiles the file as a tesselation evaluation shader
		 *	- tessControl=[filename] : Compiles the file as a tesseleation control shader
		 *	- compute=[filename] : Compiles the file as a compute shader (This option cannot be specified with any of the other file types)
		 *	- contextVersion=[version] : Sets the value for the context version
		 */

		struct ArgInfo
		{
			public bool RecompileFromFile;
			public bool EmbedAsString
			{
				get
				{
					return !(RecompileFromFile);
				}
			}
			public string OutputFile;
			public string ShaderName;
			public string Namespace;
			public Version ContextVersion;

			public List<StageItem> Stages;
		}
		public enum ShaderStage
		{
			Vertex = ShaderType.VertexShader,
			Fragment = ShaderType.FragmentShader,
			Geometry = ShaderType.GeometryShader,
			TessEval = ShaderType.TessEvaluationShader,
			TessControl = ShaderType.TessControlShader,
			Compute = ShaderType.ComputeShader
		}
		public enum UniformType
		{
			@bool,
			@double,
			Vector2d,
			Vector3d,
			Vector4d,
			@float,
			Matrix2,
			Matrix2x3,
			Matrix2x4,
			Matrix3,
			Matrix3x2,
			Matrix3x4,
			Matrix4,
			Matrix4x2,
			Matrix4x3,
			Vector2,
			Vector3,
			Vector4,
			@int,
			Texture,
			@uint
		}

		void ParseArgs(string[] args)
		{
			Info = new ArgInfo();

			Info.Stages = new List<StageItem>();
			Info.Namespace = "Shaders";
			Info.ShaderName = "__Shader" + args.GetHashCode();
			Info.ContextVersion = new Version(3, 0);

			foreach (string arg in args)
			{
				try
				{
					if (arg.StartsWith("-") || arg.StartsWith("/"))
					{
						string option = arg.Substring(1);

						if (option == "r")
						{
							Info.RecompileFromFile = true;
						}
						else if (option == "s")
						{
							Info.RecompileFromFile = false;
						}
						else if (option.StartsWith("out="))
						{
							Info.OutputFile = option.Substring(4).TrimMatchingQuotes();
						}
						else if (option.StartsWith("name="))
						{
							Info.ShaderName = option.Substring(5).TrimMatchingQuotes();
						}
						else if (option.StartsWith("vert="))
						{
							Info.Stages.Add(new StageItem(option.Substring(5).TrimMatchingQuotes(), ShaderStage.Vertex, File.ReadAllText(option.Substring(5))));
						}
						else if (option.StartsWith("frag="))
						{
							Info.Stages.Add(new StageItem(option.Substring(5).TrimMatchingQuotes(), ShaderStage.Fragment, File.ReadAllText(option.Substring(5))));
						}
						else if (option.StartsWith("geom="))
						{
							Info.Stages.Add(new StageItem(option.Substring(5).TrimMatchingQuotes(), ShaderStage.Geometry, File.ReadAllText(option.Substring(5))));
						}
						else if (option.StartsWith("tessEval="))
						{
							Info.Stages.Add(new StageItem(option.Substring(9).TrimMatchingQuotes(), ShaderStage.TessEval, File.ReadAllText(option.Substring(9))));
						}
						else if (option.StartsWith("tessControl="))
						{
							Info.Stages.Add(new StageItem(option.Substring(12).TrimMatchingQuotes(), ShaderStage.TessControl, File.ReadAllText(option.Substring(12))));
						}
						else if (option.StartsWith("compute="))
						{
							Info.Stages.Add(new StageItem(option.Substring(8).TrimMatchingQuotes(), ShaderStage.Compute, File.ReadAllText(option.Substring(8))));
						}
						else if (option.StartsWith("namespace="))
						{
							Info.Namespace = option.Substring("namespace=".Length).TrimMatchingQuotes();
						}
						else if (option.StartsWith("contextVersion="))
						{
							Info.ContextVersion = new Version(option.Substring("contextVersion=".Length).TrimMatchingQuotes());
						}
						else
						{
							Console.WriteLine("Unknown argument: '" + arg + "'");
						}
					}
					else
					{
						if (arg.EndsWith(".vert", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.Vertex, File.ReadAllText(arg)));
						}
						else if (arg.EndsWith(".frag", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.Fragment, File.ReadAllText(arg)));
						}
						else if (arg.EndsWith(".geom", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.Geometry, File.ReadAllText(arg)));
						}
						else if (arg.EndsWith(".tessEval", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.TessEval, File.ReadAllText(arg)));
						}
						else if (arg.EndsWith(".tessControl", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.TessControl, File.ReadAllText(arg)));
						}
						else if (arg.EndsWith(".compute", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.Compute, File.ReadAllText(arg)));
						}
						else if (arg.EndsWith(".vs", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.Vertex, File.ReadAllText(arg)));
						}
						else if (arg.EndsWith(".fs", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.Fragment, File.ReadAllText(arg)));
						}
						else if (arg.EndsWith(".gs", true, null))
						{
							Info.Stages.Add(new StageItem(arg, ShaderStage.Geometry, File.ReadAllText(arg)));
						}
						else
						{
							Console.WriteLine("Unable to determine stage of file: '" + arg + "' argument will be ignored.");
						}
					}
				}
				catch (FormatException)
				{
					Console.WriteLine("Invalid argument.");
				}
				catch (OverflowException)
				{
					Console.WriteLine("Invalid argument.");
				}
				catch(ArgumentOutOfRangeException)
				{
					Console.WriteLine("Invalid argument.");
				}
				catch (FileNotFoundException exec)
				{
					Console.WriteLine("Error: File \"" + exec.FileName + "\" was not found.");
				}
			}
		}

		static Version GetContextVersion()
		{
			try
			{
				return new Version(GL.GetString(StringName.Version));
			}
			catch(FormatException)
			{
				int Major = GL.GetInteger(GetPName.MajorVersion);
				int Minor = GL.GetInteger(GetPName.MinorVersion);

				return new Version(Major, Minor);
			}
		}
		static void CreateContext(Version Expected)
		{
			//Minimum version is 3.0
			if (Expected < new Version(3, 0))
				Expected = new Version(3, 0);

			//Window will stay invisible
			Window = new GameWindow(
				1080, 720, GraphicsMode.Default, "Gas Giant Test",
				GameWindowFlags.Default, DisplayDevice.Default,
				Expected.Major, Expected.Minor, GraphicsContextFlags.ForwardCompatible);
			Window.Visible = false;

			Version GLVersion = GetContextVersion();

			Console.WriteLine("Attempted to create context with version " + Expected.ToString() + ". Got version " + GLVersion.ToString() + ".");

			if (GLVersion < Expected)
			{
				//The OpenGL installation is far too old
				throw new GLVersionException(GLVersion, Expected);
			}
		}
		static void DestroyContext()
		{
			//OpenTK takes care of everything here
			Window.Dispose();
		}
		static UniformType GetUniformType(ActiveUniformType t)
		{
			switch (t)
			{
				case ActiveUniformType.Bool:
					return UniformType.@bool;
				case ActiveUniformType.Double:
					return UniformType.@double;
				case ActiveUniformType.DoubleVec2:
					return UniformType.Vector2d;
				case ActiveUniformType.DoubleVec3:
					return UniformType.Vector3d;
				case ActiveUniformType.DoubleVec4:
					return UniformType.Vector4d;
				case ActiveUniformType.Float:
					return UniformType.@float;
				case ActiveUniformType.FloatMat2:
					return UniformType.Matrix2;
				case ActiveUniformType.FloatMat2x3:
					return UniformType.Matrix2x3;
				case ActiveUniformType.FloatMat2x4:
					return UniformType.Matrix2x4;
				case ActiveUniformType.FloatMat3:
					return UniformType.Matrix3;
				case ActiveUniformType.FloatMat3x2:
					return UniformType.Matrix3x2;
				case ActiveUniformType.FloatMat3x4:
					return UniformType.Matrix3x4;
				case ActiveUniformType.FloatMat4:
					return UniformType.Matrix4;
				case ActiveUniformType.FloatMat4x2:
					return UniformType.Matrix4x2;
				case ActiveUniformType.FloatMat4x3:
					return UniformType.Matrix4x3;
				case ActiveUniformType.FloatVec2:
					return UniformType.Vector2;
				case ActiveUniformType.FloatVec3:
					return UniformType.Vector3;
				case ActiveUniformType.FloatVec4:
					return UniformType.Vector4;
				case ActiveUniformType.Image1D:
					break;
				case ActiveUniformType.Image1DArray:
					break;
				case ActiveUniformType.Image2D:
					break;
				case ActiveUniformType.Image2DArray:
					break;
				case ActiveUniformType.Image2DMultisample:
					break;
				case ActiveUniformType.Image2DMultisampleArray:
					break;
				case ActiveUniformType.Image2DRect:
					break;
				case ActiveUniformType.Image3D:
					break;
				case ActiveUniformType.ImageBuffer:
					break;
				case ActiveUniformType.ImageCube:
					break;
				case ActiveUniformType.ImageCubeMapArray:
					break;
				case ActiveUniformType.Int:
					return UniformType.@int;
				case ActiveUniformType.IntImage1D:
					break;
				case ActiveUniformType.IntImage1DArray:
					break;
				case ActiveUniformType.IntImage2D:
					break;
				case ActiveUniformType.IntImage2DArray:
					break;
				case ActiveUniformType.IntImage2DMultisample:
					break;
				case ActiveUniformType.IntImage2DMultisampleArray:
					break;
				case ActiveUniformType.IntImage2DRect:
					break;
				case ActiveUniformType.IntImage3D:
					break;
				case ActiveUniformType.IntImageBuffer:
					break;
				case ActiveUniformType.IntImageCube:
					break;
				case ActiveUniformType.IntImageCubeMapArray:
					break;
				case ActiveUniformType.IntSampler1D:
				case ActiveUniformType.IntSampler1DArray:
				case ActiveUniformType.IntSampler2D:
				case ActiveUniformType.IntSampler2DArray:
				case ActiveUniformType.IntSampler2DMultisample:
				case ActiveUniformType.IntSampler2DMultisampleArray:
				case ActiveUniformType.IntSampler2DRect:
				case ActiveUniformType.IntSampler3D:
				case ActiveUniformType.IntSamplerBuffer:
				case ActiveUniformType.IntSamplerCube:
				case ActiveUniformType.IntSamplerCubeMapArray:
					return UniformType.Texture;
				case ActiveUniformType.IntVec2:
					break;
				case ActiveUniformType.IntVec3:
					break;
				case ActiveUniformType.IntVec4:
					break;
				case ActiveUniformType.Sampler1D:
				case ActiveUniformType.Sampler1DArray:
				case ActiveUniformType.Sampler1DArrayShadow:
				case ActiveUniformType.Sampler1DShadow:
				case ActiveUniformType.Sampler2D:
				case ActiveUniformType.Sampler2DArray:
				case ActiveUniformType.Sampler2DArrayShadow:
				case ActiveUniformType.Sampler2DMultisample:
				case ActiveUniformType.Sampler2DMultisampleArray:
				case ActiveUniformType.Sampler2DRect:
				case ActiveUniformType.Sampler2DRectShadow:
				case ActiveUniformType.Sampler2DShadow:
				case ActiveUniformType.Sampler3D:
				case ActiveUniformType.SamplerBuffer:
				case ActiveUniformType.SamplerCube:
				case ActiveUniformType.SamplerCubeMapArray:
				case ActiveUniformType.SamplerCubeMapArrayShadow:
				case ActiveUniformType.SamplerCubeShadow:
					return UniformType.Texture;
				case ActiveUniformType.UnsignedInt:
					return UniformType.@uint;
				case ActiveUniformType.UnsignedIntAtomicCounter:
					break;
				case ActiveUniformType.UnsignedIntImage1D:
					break;
				case ActiveUniformType.UnsignedIntImage1DArray:
					break;
				case ActiveUniformType.UnsignedIntImage2D:
					break;
				case ActiveUniformType.UnsignedIntImage2DArray:
					break;
				case ActiveUniformType.UnsignedIntImage2DMultisample:
					break;
				case ActiveUniformType.UnsignedIntImage2DMultisampleArray:
					break;
				case ActiveUniformType.UnsignedIntImage2DRect:
					break;
				case ActiveUniformType.UnsignedIntImage3D:
					break;
				case ActiveUniformType.UnsignedIntImageBuffer:
					break;
				case ActiveUniformType.UnsignedIntImageCube:
					break;
				case ActiveUniformType.UnsignedIntImageCubeMapArray:
					break;
				case ActiveUniformType.UnsignedIntSampler1D:
				case ActiveUniformType.UnsignedIntSampler1DArray:
				case ActiveUniformType.UnsignedIntSampler2D:
				case ActiveUniformType.UnsignedIntSampler2DArray:
				case ActiveUniformType.UnsignedIntSampler2DMultisample:
				case ActiveUniformType.UnsignedIntSampler2DMultisampleArray:
				case ActiveUniformType.UnsignedIntSampler2DRect:
				case ActiveUniformType.UnsignedIntSampler3D:
				case ActiveUniformType.UnsignedIntSamplerBuffer:
				case ActiveUniformType.UnsignedIntSamplerCube:
				case ActiveUniformType.UnsignedIntSamplerCubeMapArray:
					return UniformType.Texture;
				case ActiveUniformType.UnsignedIntVec2:
					break;
				case ActiveUniformType.UnsignedIntVec3:
					break;
				case ActiveUniformType.UnsignedIntVec4:
					break;
				default:
					break;
			}

			throw new Exception("Type: " + t.ToString() + " is not supported at this time.");
		}
		static string GetDrawCommand(ActiveUniformType t, string name)
		{
			switch (t)
			{
				case ActiveUniformType.Int:
				case ActiveUniformType.UnsignedInt:
				case ActiveUniformType.Double:
				case ActiveUniformType.Float:
				case ActiveUniformType.Bool:
					return "GL.Uniform1(__" + name + ", uniform_" + name + ");";
				case ActiveUniformType.BoolVec2:
					break;
				case ActiveUniformType.BoolVec3:
					break;
				case ActiveUniformType.BoolVec4:
					break;
				case ActiveUniformType.DoubleVec2:
				case ActiveUniformType.FloatVec2:
					return "GL.Uniform2(__" + name + ", uniform_" + name + ");";
				case ActiveUniformType.FloatVec3:
				case ActiveUniformType.DoubleVec3:
					return "GL.Uniform3(__" + name + ", uniform_" + name + ");";
				case ActiveUniformType.FloatVec4:
				case ActiveUniformType.DoubleVec4:
					return "GL.Uniform4(__" + name + ", uniform_" + name + ");";
				case ActiveUniformType.FloatMat2:
					return "GL.UniformMatrix2(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.FloatMat2x3:
					return "GL.UniformMatrix2x3(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.FloatMat2x4:
					return "GL.UniformMatrix2x4(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.FloatMat3:
					return "GL.UniformMatrix3(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.FloatMat3x2:
					return "GL.UniformMatrix3x2(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.FloatMat3x4:
					return "GL.UniformMatrix3x4(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.FloatMat4:
					return "GL.UniformMatrix4(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.FloatMat4x2:
					return "GL.UniformMatrix4x2(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.FloatMat4x3:
					return "GL.UniformMatrix4x3(__" + name + ", TransposeMatrix, ref uniform_" + name + ");";
				case ActiveUniformType.Image1D:
					break;
				case ActiveUniformType.Image1DArray:
					break;
				case ActiveUniformType.Image2D:
					break;
				case ActiveUniformType.Image2DArray:
					break;
				case ActiveUniformType.Image2DMultisample:
					break;
				case ActiveUniformType.Image2DMultisampleArray:
					break;
				case ActiveUniformType.Image2DRect:
					break;
				case ActiveUniformType.Image3D:
					break;
				case ActiveUniformType.ImageBuffer:
					break;
				case ActiveUniformType.ImageCube:
					break;
				case ActiveUniformType.ImageCubeMapArray:
					break;
				case ActiveUniformType.IntImage1D:
					break;
				case ActiveUniformType.IntImage1DArray:
					break;
				case ActiveUniformType.IntImage2D:
					break;
				case ActiveUniformType.IntImage2DArray:
					break;
				case ActiveUniformType.IntImage2DMultisample:
					break;
				case ActiveUniformType.IntImage2DMultisampleArray:
					break;
				case ActiveUniformType.IntImage2DRect:
					break;
				case ActiveUniformType.IntImage3D:
					break;
				case ActiveUniformType.IntImageBuffer:
					break;
				case ActiveUniformType.IntImageCube:
					break;
				case ActiveUniformType.IntImageCubeMapArray:
					break;
				case ActiveUniformType.IntSampler1D:
					break;
				case ActiveUniformType.IntSampler1DArray:
					break;
				case ActiveUniformType.IntSampler2D:
					break;
				case ActiveUniformType.IntSampler2DArray:
					break;
				case ActiveUniformType.IntSampler2DMultisample:
					break;
				case ActiveUniformType.IntSampler2DMultisampleArray:
					break;
				case ActiveUniformType.IntSampler2DRect:
					break;
				case ActiveUniformType.IntSampler3D:
					break;
				case ActiveUniformType.IntSamplerBuffer:
					break;
				case ActiveUniformType.IntSamplerCube:
					break;
				case ActiveUniformType.IntSamplerCubeMapArray:
					break;
				case ActiveUniformType.IntVec2:
					break;
				case ActiveUniformType.IntVec3:
					break;
				case ActiveUniformType.IntVec4:
					break;
				case ActiveUniformType.UnsignedIntAtomicCounter:
					break;
				case ActiveUniformType.UnsignedIntImage1D:
					break;
				case ActiveUniformType.UnsignedIntImage1DArray:
					break;
				case ActiveUniformType.UnsignedIntImage2D:
					break;
				case ActiveUniformType.UnsignedIntImage2DArray:
					break;
				case ActiveUniformType.UnsignedIntImage2DMultisample:
					break;
				case ActiveUniformType.UnsignedIntImage2DMultisampleArray:
					break;
				case ActiveUniformType.UnsignedIntImage2DRect:
					break;
				case ActiveUniformType.UnsignedIntImage3D:
					break;
				case ActiveUniformType.UnsignedIntImageBuffer:
					break;
				case ActiveUniformType.UnsignedIntImageCube:
					break;
				case ActiveUniformType.UnsignedIntImageCubeMapArray:
					break;
				case ActiveUniformType.UnsignedIntSampler1D:
					break;
				case ActiveUniformType.UnsignedIntSampler1DArray:
					break;
				case ActiveUniformType.UnsignedIntSampler2D:
					break;
				case ActiveUniformType.UnsignedIntSampler2DArray:
					break;
				case ActiveUniformType.UnsignedIntSampler2DMultisample:
					break;
				case ActiveUniformType.UnsignedIntSampler2DMultisampleArray:
					break;
				case ActiveUniformType.UnsignedIntSampler2DRect:
					break;
				case ActiveUniformType.UnsignedIntSampler3D:
					break;
				case ActiveUniformType.UnsignedIntSamplerBuffer:
					break;
				case ActiveUniformType.UnsignedIntSamplerCube:
					break;
				case ActiveUniformType.UnsignedIntSamplerCubeMapArray:
					break;
				case ActiveUniformType.UnsignedIntVec2:
					break;
				case ActiveUniformType.UnsignedIntVec3:
					break;
				case ActiveUniformType.UnsignedIntVec4:
					break;
				default:
					break;
			}

			return "";
		}
		static string ToString(UniformType t)
		{
			switch (t)
			{
				case UniformType.@bool:
				case UniformType.@double:
				case UniformType.Vector2d:
				case UniformType.@float:
				case UniformType.@int:
				case UniformType.@uint:
					return t.ToString();
				case UniformType.Vector3d:
				case UniformType.Vector4d:
				case UniformType.Matrix2:
				case UniformType.Matrix2x3:
				case UniformType.Matrix2x4:
				case UniformType.Matrix3:
				case UniformType.Matrix3x2:
				case UniformType.Matrix3x4:
				case UniformType.Matrix4:
				case UniformType.Matrix4x2:
				case UniformType.Matrix4x3:
				case UniformType.Vector2:
				case UniformType.Vector3:
				case UniformType.Vector4:
					return "global::OpenTK." + t.ToString();
				case UniformType.Texture:
					return "global::ShaderRuntime.Texture";
				default:
					return "";
			}
		}

		static string[] SplitCommandLine(string CommandLine)
		{
			bool inQuotes = false;
			bool isEscaping = false;

			return CommandLine.SplitWhere(c =>
				{
					if (c == '\\' && !isEscaping)
					{
						isEscaping = true;
						return false;
					}

					if (c == '\"' && !isEscaping)
					{
						inQuotes = !inQuotes;
					}

					isEscaping = false;

					return !inQuotes && Char.IsWhiteSpace(c);
				})
				.Select(arg => arg.Trim().TrimMatchingQuotes().Replace("\\\"", "\""))
				.Where(arg => !string.IsNullOrEmpty(arg))
				.ToArray();

		}

		bool TestCompile()
		{
			int ShaderID = 0;

			//For stage ids
			List<int> Stages = new List<int>();

			ShaderID = GL.CreateProgram();

			bool IsCompute = false;
			bool Failed = false;

			foreach (StageItem stage in Info.Stages)
			{
				//Check whether the current stage is a compute shader
				IsCompute = IsCompute || stage.Item2 == ShaderStage.Compute;

				//Compute shaders aren't allowed to be compiled with other shader types
				if (IsCompute && stage.Item2 != ShaderStage.Compute)
				{
					//Error and exit
					Console.WriteLine("Error: Compute shaders cannot be compiled with other shader types.");
					return false;
				}

				//Create the shader stage and compile it
				int id = GL.CreateShader((ShaderType)stage.Item2);

				GL.ShaderSource(id, stage.Item3);
				GL.CompileShader(id);
				GL.AttachShader(ShaderID, id);

				int CompileStatus = 0;

				GL.GetShader(id, ShaderParameter.CompileStatus, out CompileStatus);

				//Check for errors
				if (CompileStatus == 0)
				{
					Console.WriteLine("Shader Failed to compile. Info log: \n" + Regex.Replace(GL.GetShaderInfoLog(id), "0\\(", stage.Item1 + "("));
					Failed = true;
				}

				Stages.Add(id);
			}

			GL.LinkProgram(ShaderID);

			//Delete and destroy all shader stages
			foreach (int shader in Stages)
			{
				GL.DetachShader(ShaderID, shader);
				GL.DeleteShader(shader);
			}

			//Check if a shader stage compilation failed
			if (Failed)
			{
				Console.WriteLine("Shader failed to compile. Exiting.");
				return false;
			}

			//Check for link errors
			int LinkStatus;
			GL.GetProgram(ShaderID, GetProgramParameterName.LinkStatus, out LinkStatus);
			string InfoLog = GL.GetProgramInfoLog(ShaderID);

			//See whether linking failed
			if (LinkStatus == 0)
			{
				Console.WriteLine("Shader failed to link. Info log: \n" + InfoLog);
				return false;
			}

			int Count = 0;

			GL.GetProgram(ShaderID, GetProgramParameterName.ActiveAttributes, out Count);

			//Get all attributes
			for (int i = 0; i < Count; i++)
			{
				int size;
				int length;
				ActiveAttribType type;
				StringBuilder name = new StringBuilder(512);

				GL.GetActiveAttrib(ShaderID, i, 512, out length, out size, out type, name);

				Attributes.Add(new Tuple<ActiveAttribType, string>(type, name.ToString()));
			}

			GL.GetProgram(ShaderID, GetProgramParameterName.ActiveUniforms, out Count);

			//Get all uniforms
			for (int i = 0; i < Count; i++)
			{
				int size;
				int length;
				ActiveUniformType type;
				StringBuilder name = new StringBuilder(512);

				GL.GetActiveUniform(ShaderID, i, 512, out length, out size, out type, name);

				Uniforms.Add(new Tuple<ActiveUniformType, string>(type, name.ToString()));
			}

			//Get rid of the program
			GL.DeleteProgram(ShaderID);

			return true;
		}
		void WriteToFile()
		{
			int Counter = 0;
			List<string> Lines = new List<string>();
			List<string> InitCommands = new List<string>();
			List<string> DrawCommands = new List<string>();

			foreach (UniformItem item in Uniforms)
			{
				if (GetUniformType(item.Item1) == UniformType.Texture)
				{
					InitCommands.Add("GL.ActiveTexture(global::OpenTK.Graphics.OpenGL.TextureUnit.Texture" + Counter + ");");
					InitCommands.Add("GL.BindTexture(uniform_" + item.Item2 + ".Target, uniform_" + item.Item2 + ".TextureID);");
					DrawCommands.Add("GL.Uniform1(__" + item.Item2 + ", " + Counter + ");");
					Counter++;
				}
				else
				{
					DrawCommands.Add(GetDrawCommand(item.Item1, item.Item2));
				}
			}

			/*
			 *	Naming Conventions
			 *		- IDs : Start with double underscore ('__')
			 *		- Uniform Variables : Start with 'uniform_'
			 *		- Attributes : Aren't done by the compiler.
			 *		- Shader source : [stage]Source
			 */

			//Standard warning
			Lines.Add("// <auto-generated>");
			Lines.Add("//\tThis code was generated by a Tool.");
			Lines.Add("//");
			Lines.Add("//\tChanges to this file may cause incorrect behavior and will be lost if");
			Lines.Add("//\tthe code is regenerated.");
			Lines.Add("// <auto-generated>");
			Lines.Add("");

			#region Usings
			Lines.Add("using System;");
			Lines.Add("using GL = global::OpenTK.Graphics.OpenGL.GL;");
			#endregion

			//Namespace and class start
			Lines.Add("");
			Lines.Add("#pragma warning disable 168");
			Lines.Add("");
			Lines.Add("namespace " + Info.Namespace);
			Lines.Add("{");
			Lines.Add("\t[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"ShaderCompiler.exe\", \"" + CompilerVersion.ToString() + "\")]");
			Lines.Add("\tpublic class " + Info.ShaderName + " : global::ShaderRuntime.GLShader");
			Lines.Add("\t{");

			#region ImplementationSupportsShaders
			Lines.Add("\t\tpublic static bool ImplementationSupportsShaders");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\tget");
			Lines.Add("\t\t\t{");
			Lines.Add("\t\t\t\treturn (new Version(GL.GetString(global::OpenTK.Graphics.OpenGL.StringName.Version).Substring(0, 3)) >= new Version(2, 0) ? true : false);");
			Lines.Add("\t\t\t}");
			Lines.Add("\t\t}");
			#endregion
			#region Fields
			Lines.Add("\t\tstatic int ProgramID;");
			Lines.Add("\t\tprivate static global::ShaderRuntime.Utility.Counter Ctr = new global::ShaderRuntime.Utility.Counter(new Action(delegate{ GL.DeleteProgram(ProgramID); ProgramID = 0; }));");
			Lines.Add("\t\tpublic bool TransposeMatrix = false;");
			foreach (Tuple<ActiveUniformType, string> uniform in Uniforms)
			{
				Lines.Add("\t\tpublic static int __" + uniform.Item2 + ";");

				Lines.Add("\t\tpublic " + ToString(GetUniformType(uniform.Item1)) + " uniform_" + uniform.Item2 + ";");
			}

			foreach (Tuple<ActiveAttribType, string> attrib in Attributes)
			{
				Lines.Add("\t\tpublic static int __" + attrib.Item2 + ";");
			}
			#endregion
			#region CompileShader
			foreach (StageItem stage in Info.Stages)
			{
				Lines.Add("\t\tprivate static string " + stage.Item2.ToString() + "Source = \"" + Regex.Replace(stage.Item3, "(\r\n|\r|\n)", "\\n") + "\";");
			}

			#region UpdateShaderCode
			if (Info.RecompileFromFile)
			{
				Lines.Add("\t\tprivate static void LoadShaders()");
				Lines.Add("\t\t{");
				foreach (StageItem stage in Info.Stages)
				{
					Lines.Add("\t\t\t" + stage.Item2.ToString() + "Source = global::System.IO.File.ReadAllText(@\"" + stage.Item1 + "\");");
				}
				Lines.Add("\t\t}");
			}
			#endregion

			Lines.Add("\t\tpublic static void CompileShader()");
			Lines.Add("\t\t{");
			if (Info.RecompileFromFile)
			{
				Lines.Add("\t\t\tLoadShaders();");
			}
			Lines.Add("\t\t\tProgramID = GL.CreateProgram();");

			foreach (StageItem stage in Info.Stages)
			{
				Lines.Add("\t\t\tint " + stage.Item2.ToString() + " = GL.CreateShader(global::OpenTK.Graphics.OpenGL.ShaderType." + ((ShaderType)stage.Item2).ToString() + ");");
				Lines.Add("\t\t\tGL.ShaderSource(" + stage.Item2.ToString() + ", " + stage.Item2.ToString() + "Source);");
				Lines.Add("\t\t\tGL.CompileShader(" + stage.Item2.ToString() + ");");
				Lines.Add("\t\t\tGL.AttachShader(ProgramID, " + stage.Item2.ToString() + ");");
			}
			Lines.Add("\t\t\tGL.LinkProgram(ProgramID);");

			Lines.Add("\t\t\tglobal::System.Diagnostics.Debug.WriteLine(GL.GetProgramInfoLog(ProgramID));");

			foreach (StageItem stage in Info.Stages)
			{
				Lines.Add("\t\t\tGL.DetachShader(ProgramID, " + stage.Item2.ToString() + ");");
				Lines.Add("\t\t\tGL.DeleteShader(" + stage.Item2.ToString() + ");");
			}

			foreach (Tuple<ActiveUniformType, string> uniform in Uniforms)
			{
				Lines.Add("\t\t\t__" + uniform.Item2 + " = GL.GetUniformLocation(ProgramID, \"" + uniform.Item2 + "\");");
			}

			foreach (Tuple<ActiveAttribType, string> attrib in Attributes)
			{
				Lines.Add("\t\t\t__" + attrib.Item2 + " = GL.GetAttribLocation(ProgramID, \"" + attrib.Item2 + "\");");
			}

			Lines.Add("\t\t}");

			Lines.Add("\t\tpublic void Recompile()");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\tGL.DeleteShader(ProgramID);");
			Lines.Add("\t\t\tProgramID = 0;");
			Lines.Add("\t\t\tCompile();");
			Lines.Add("\t\t}");

			Lines.Add("\t\tpublic void Compile()");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\tif(ProgramID == 0)");
			Lines.Add("\t\t\t\tCompileShader();");
			Lines.Add("\t\t\tCtr++;");
			Lines.Add("\t\t}");
			#endregion
			#region SetParameter
			Lines.Add("\t\tpublic void SetParameter<T>(string name, T value)");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\ttry");
			Lines.Add("\t\t\t{");
			Lines.Add("\t\t\t\tswitch(name)");
			Lines.Add("\t\t\t\t{");

			foreach (Tuple<ActiveUniformType, string> uniform in Uniforms)
			{
				Lines.Add("\t\t\t\t\tcase \"" + uniform.Item2 + "\":");
				Lines.Add("\t\t\t\t\t\tuniform_" + uniform.Item2 + " = (" + ToString(GetUniformType(uniform.Item1)) + ")(object)value;");
				Lines.Add("\t\t\t\t\t\tbreak;");
			}

			Lines.Add("\t\t\t\t\tdefault:");
			Lines.Add("\t\t\t\t\t\tthrow new global::ShaderRuntime.InvalidIdentifierException(\"There is no uniform variable named \" + name + \" in this shader.\");");
			Lines.Add("\t\t\t\t}");
			Lines.Add("\t\t\t}");
			Lines.Add("\t\t\tcatch(InvalidCastException e)");
			Lines.Add("\t\t\t{");
			Lines.Add("\t\t\t\tthrow new global::ShaderRuntime.InvalidParameterTypeException(\"Invalid parameter type: \" + name + \" is not convertible from the type \\\"\" + typeof(T).FullName + \"\\\".\");");
			Lines.Add("\t\t\t}");
			Lines.Add("\t\t}");
			#endregion
			#region GetParameter
			Lines.Add("\t\tpublic T GetParameter<T>(string name)");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\ttry");
			Lines.Add("\t\t\t{");
			Lines.Add("\t\t\t\tswitch(name)");
			Lines.Add("\t\t\t\t{");

			foreach (Tuple<ActiveUniformType, string> uniform in Uniforms)
			{
				Lines.Add("\t\t\t\t\tcase \"" + uniform.Item2 + "\":");
				Lines.Add("\t\t\t\t\t\treturn (T)(object)uniform_" + uniform.Item2 + ";");
			}

			Lines.Add("\t\t\t\t\tdefault:");
			Lines.Add("\t\t\t\t\t\tthrow new global::ShaderRuntime.InvalidIdentifierException(\"There is no uniform variable named \" + name + \" in this shader.\");");
			Lines.Add("\t\t\t\t}");
			Lines.Add("\t\t\t}");
			Lines.Add("\t\t\tcatch(InvalidCastException e)");
			Lines.Add("\t\t\t{");
			Lines.Add("\t\t\t\tthrow new global::ShaderRuntime.InvalidParameterTypeException(\"Invalid paramater type: \" + name + \" is not convertible to the type \\\"\" + typeof(T).FullName + \"\\\".\");");
			Lines.Add("\t\t\t}");
			Lines.Add("\t\t}");
			#endregion
			#region GetParameterLocation
			Lines.Add("\t\tpublic int GetParameterLocation(string name)");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\tswitch(name)");
			Lines.Add("\t\t\t{");

			foreach (Tuple<ActiveUniformType, string> uniform in Uniforms)
			{
				Lines.Add("\t\t\t\tcase \"" + uniform.Item2 + "\":");
				Lines.Add("\t\t\t\t\treturn __" + uniform.Item2 + ";");
			}

			foreach (Tuple<ActiveAttribType, string> attrib in Attributes)
			{
				Lines.Add("\t\t\t\tcase \"" + attrib.Item2 + "\":");
				Lines.Add("\t\t\t\t\treturn __" + attrib.Item2 + ";");
			}

			Lines.Add("\t\t\t\tdefault:");
			Lines.Add("\t\t\t\t\tthrow new global::ShaderRuntime.InvalidIdentifierException(\"There is no parameter named \" + name + \".\");");
			Lines.Add("\t\t\t}");
			Lines.Add("\t\t}");
			#endregion
			#region PassUniforms
			Lines.Add("\t\tpublic void PassUniforms()");
			Lines.Add("\t\t{");

			foreach (string str in DrawCommands)
			{
				Lines.Add("\t\t\t" + str);
			}

			Lines.Add("\t\t}");
			#endregion
			#region UseShader
			Lines.Add("\t\tpublic void UseShader()");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\tGL.UseProgram(ProgramID);");

			foreach (string str in InitCommands)
			{
				Lines.Add("\t\t\t" + str);
			}

			foreach (Tuple<ActiveAttribType, string> attrib in Attributes)
			{
				Lines.Add("\t\t\tGL.EnableVertexAttribArray(__" + attrib.Item2 + ");");
			}

			Lines.Add("\t\t}");
			#endregion
			#region GetShaderID
			Lines.Add("\t\tpublic int GetShaderID()");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\tif(ProgramID != 0)");
			Lines.Add("\t\t\t\treturn ProgramID;");
			Lines.Add("\t\t\tthrow new global::ShaderRuntime.ShaderNotInitializedException(\"The shader \\\"" + Info.ShaderName + "\\\" has not been initialized. Call Compile() on one of the instances or CompileShader() to compile the shader\");");
			Lines.Add("\t\t}");
			#endregion
			#region Dispose
			Lines.Add("\t\tpublic void Dispose()");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\tCtr--;");
			Lines.Add("\t\t}");
			#endregion
			#region IsSupported
			Lines.Add("\t\tpublic bool IsSupported");
			Lines.Add("\t\t{");
			Lines.Add("\t\t\tget");
			Lines.Add("\t\t\t{");
			Lines.Add("\t\t\t\treturn ImplementationSupportsShaders;");
			Lines.Add("\t\t\t}");
			Lines.Add("\t\t}");
			#endregion
			#region GetUniformNames
			Lines.Add("\t\tpublic global::System.Collections.Generic.IEnumerable<string> GetUniformNames()");
			Lines.Add("\t\t{");
			foreach (UniformItem uniform in Uniforms)
			{
				Lines.Add("\t\t\tyield return \"" + uniform.Item2 + "\";");
			}
			Lines.Add("\t\t}");
			#endregion

			//End class and namespace scope
			Lines.Add("\t}");
			Lines.Add("}");

			//Write to the output file
			File.WriteAllLines(Info.OutputFile, Lines);
		}

		/// <summary>
		/// Called when /multicompile is not specified
		/// </summary>
		/// <param name="args"></param>
		static int Compile(string[] args)
		{
			if (args.Length < 1)
			{
				Console.WriteLine("No arguments passed.");
				return -1;
			}
			else if (args[0] == "/help" || args[0] == "-help")
			{
				Console.WriteLine("Arguments: (May be prefixed with - or /)"
					 + "\n  - r : Reloads the shader from files in the current directory whenever the shader is compiled"
					 + "\n  - s : Embeds the shader as strings in the output file (this is the default)"
					 + "\n  - out=[filename] : Sets the output file (The default is to use the first input file's name)"
					 + "\n  - name=[string] : Sets the name of the shader (This will be the name of the output class)"
					 + "\n  - [filename] : One of the files to compile as a shader stage (The stage of the shader is inferred from the extension)"
					 + "\n  - vert=[filename] : Compiles the file as a vertex shader"
					 + "\n  - frag=[filename] : Compiles the file as a fragment shader"
					 + "\n  - geom=[filename] : Compiles the file as a geometry shader"
					 + "\n  - tessEval=[filename] : Compiles the file as a tesselation evaluation shader"
					 + "\n  - tessControl=[filename] : Compiles the file as a tesselation control shader"
					 + "\n  - compute=[filename] : Compiles the file as a compute shader (This option cannot be specified with any of the other file types)"
					 + "\n  - namespace=[string] : Sets the namespace of the shader (The default is 'Shaders')");
				return 0;
			}

			Program p = new Program();
			p.ParseArgs(args);
			CreateContext(p.Info.ContextVersion);
			try
			{
				if (p.TestCompile())
				{
					p.WriteToFile();
					return 0;
				}
				return -1;
			}
			finally
			{
				DestroyContext();
			}
		}

		static int ProgramMain(string[] args)
		{
			try
			{
				if (args.Length < 1)
				{
					Console.WriteLine("No arguments passed.");
					return -1;
				}
				else if (args[0] == "/help" || args[0] == "-help")
				{
					Console.WriteLine("Arguments: (May be prefixed with - or /)"
						 + "\n  - r : Reloads the shader from files in the current directory whenever the shader is compiled"
						 + "\n  - s : Embeds the shader as strings in the output file (this is the default)"
						 + "\n  - out=[filename] : Sets the output file (The default is to use the first input file's name)"
						 + "\n  - name=[string] : Sets the name of the shader (This will be the name of the output class)"
						 + "\n  - [filename] : One of the files to compile as a shader stage (The stage of the shader is inferred from the extension)"
						 + "\n  - vert=[filename] : Compiles the file as a vertex shader"
						 + "\n  - frag=[filename] : Compiles the file as a fragment shader"
						 + "\n  - geom=[filename] : Compiles the file as a geometry shader"
						 + "\n  - tessEval=[filename] : Compiles the file as a tesselation evaluation shader"
						 + "\n  - tessControl=[filename] : Compiles the file as a tesselation control shader"
						 + "\n  - compute=[filename] : Compiles the file as a compute shader (This option cannot be specified with any of the other file types)"
						 + "\n  - namespace=[string] : Sets the namespace of the shader (The default is 'Shaders')"
						 + "\n  - contextVersion=[version] : Set the OpenGL context version. The minimum and default versions are 3.0.");
					return 0;
				}
				else
				{
					return Compile(args);
				}
			}
			catch (GLVersionException e)
			{
				Console.WriteLine("OpenGL version is not high enough. Expected OpenGL version " + e.Expected + " or greater, got version " + e.Version + ".");
				return 0;
			}
		}

		static int Main(string[] args)
		{
			return ProgramMain(args);
		}
	}
}
