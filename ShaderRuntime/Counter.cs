using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderRuntime
{
    namespace Utility
    {
		/// <summary>
		/// A utility class for reference tracking
		/// </summary>
        public class Counter
        {
            private uint InternalCount;
            private Action DestructAction;

			/// <summary>
			/// The number of references left.
			/// </summary>
            public uint Count
            {
                get
                {
                    return InternalCount;
                }
            }
			/// <summary>
			/// The action that is executed when <see cref="Count"/> reaches 0.
			/// </summary>
            public Action Destructor
            {
                get
                {
                    return DestructAction;
                }
                set
                {
                    DestructAction = value;
                }
            }

			/// <summary>
			/// Creates a <see cref="Counter"/> with the given destructor action.
			/// </summary>
			/// <param name="Destructor">An action that will be executed when <see cref="Count"/> reaches 0.</param>
            public Counter(Action Destructor)
            {
                DestructAction = Destructor;
            }

			/// <summary>
			/// Increments the value of the counter.
			/// </summary>
            public void Increment()
            {
                InternalCount++;
            }
			/// <summary>
			/// Decrements the value of the counter and executes the Destructor if <see cref="Count"/> is equal to 0.
			/// </summary>
            public void Decrement()
            {
                if (--InternalCount == 0)
                    DestructAction();
            }

			/// <summary>
			/// Increments the counter.
			/// </summary>
			/// <param name="var"></param>
			/// <returns></returns>
            public static Counter operator++(Counter var)
            {
                var.Increment();
                return var;
            }
			/// <summary>
			/// Decrements the counter.
			/// </summary>
			/// <param name="var"></param>
			/// <returns></returns>
            public static Counter operator--(Counter var)
            {
                var.Decrement();
                return var;
            }
        }
    }
    
}
