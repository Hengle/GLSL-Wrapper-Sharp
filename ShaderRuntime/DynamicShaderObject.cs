using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;
using System.Diagnostics;

namespace ShaderRuntime
{
#pragma warning disable 168

	/// <summary>
	/// Allows for shaders uniforms to be accessed using member syntax.
	/// Note that it is not possible for members to be added or removed using this syntax. 
	/// Trying to set members that are not part of the shader will throw an exception
	/// and trying to set members with incorrect types will throw an exception.
	/// </summary>
	/// <example>
	///	Instead of setting shader variables like this: <code>MyShader.SetParameter("ParameterName", value);</code>
	///	you can now set them like this: <code>dynamic Shader = new DynamicShaderObject(MyShader); Shader.ParameterName = value;</code>
	/// </example>
	public class DynamicShaderObject : DynamicObject
	{
		/// <summary>
		/// The current shader that this DynamicShaderObject is referencing.
		/// </summary>
		public GLShader Shader;

		/* Implements DynamicObject */
#pragma warning disable 1591

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			try
			{
				result = Shader.GetParameter<object>(binder.Name);
			}
			catch(InvalidIdentifierException e)
			{
				result = null;
#if !DEBUG
				return false;
#else
				Debug.WriteLine("Shader does not contain identifier " + binder.Name + ".");
#endif
			}
			return true;
		}
		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			try
			{
				Shader.SetParameter<object>(binder.Name, value);
			}
			catch(InvalidIdentifierException e)
			{
#if !DEBUG
				return false;
#else
				Debug.WriteLine("Shader does not contain identifier " + binder.Name + ".");
#endif
			}
			catch(InvalidParameterTypeException e)
			{
#if !DEBUG
				return false;
#else
				Debug.WriteLine("Shader does not contain identifier " + binder.Name + ".");
#endif
			}
			return true;
		}
		public override IEnumerable<string> GetDynamicMemberNames()
		{
			return Shader.GetUniformNames();
		}

		/// <summary>
		/// Creates a shader object that accesses the given shader.
		/// </summary>
		/// <param name="Shader"></param>
		public DynamicShaderObject(GLShader Shader)
		{
			this.Shader = Shader;
		}
	}
}
