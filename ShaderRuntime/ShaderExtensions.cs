using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable 168

namespace ShaderRuntime
{
	public static class ShaderExtensions
	{
		/// <summary>
		/// Sets the parameter with an object value.
		/// </summary>
		/// <param name="shader"></param>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public static void SetParameter(this GLShader shader, string name, object value)
		{
			shader.SetParameter(name, value);
		}
		/// <summary>
		/// Sets the parameter and catches any exception if it fails.
		/// </summary>
		/// <param name="shader"></param>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public static void SetParameterSafe(this GLShader shader, string name, object value)
		{
			try
			{
				SetParameter(shader, name, value);
			}
			catch(InvalidIdentifierException e)
			{
				
			}
			catch(InvalidParameterTypeException e)
			{

			}
		}
		/// <summary>
		/// Returns the parameter as an object value.
		/// </summary>
		/// <param name="shader"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static object GetParameter(this GLShader shader, string name)
		{
			return shader.GetParameter<Object>(name);
		}
		/// <summary>
		/// Returns the location of the uniform variable in the shader.
		/// </summary>
		/// <param name="shader"></param>
		/// <param name="name">The name of the parameter.</param>
		/// <returns>The location of the uniform variable, or -1.</returns>
		public static int GetParameterLocationSafe(this GLShader shader, string name)
		{
			try
			{
				return shader.GetParameterLocation(name);
			}
			catch (InvalidIdentifierException e)
			{
				return -1;
			}
		}
	}
}
