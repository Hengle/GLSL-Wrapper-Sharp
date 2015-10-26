using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;

namespace ShaderRuntime
{
    /// <summary>
    /// Represents an OpenGL texture. It does not manage it in any way.
    /// </summary>
    public class Texture
    {
        /// <summary>
        /// The OpenGL texture id.
        /// </summary>
        public int TextureID;
        /// <summary>
        /// The OpenGL texture target.
        /// </summary>
        public TextureTarget Target;

        /// <summary>
        /// Creates the texture with the given target and OpenGL ID.
        /// </summary>
        /// <param name="Tex"> The OpenGL texture ID. </param>
        /// <param name="Tgt"> The texture target. </param>
        public Texture(int Tex, TextureTarget Tgt)
        {
            TextureID = Tex;
            Target = Tgt;
        }

		public Texture()
		{
			TextureID = default(int);
			Target = default(TextureTarget);
		}

        /// <summary>
        /// Whether this texture is valid.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return TextureID != 0;
            }
        }

		/// <summary>
		/// The same as doing <code>GL.BindTexture(Target, TextureID)</code>
		/// </summary>
		public void Bind()
		{
			GL.BindTexture(Target, TextureID);
		}
		/// <summary>
		/// Equivalent to <code>GL.DeleteTexture(TextureID)</code>
		/// </summary>
		public void Delete()
		{
			GL.DeleteTexture(TextureID);
		}

		/// <summary>
		/// Generates a new texture.
		/// </summary>
		/// <param name="tgt">The texture target.</param>
		/// <returns></returns>
		public static Texture GenTexture(TextureTarget tgt)
		{
			return new Texture(GL.GenTexture(), tgt);
		}
    }
}
