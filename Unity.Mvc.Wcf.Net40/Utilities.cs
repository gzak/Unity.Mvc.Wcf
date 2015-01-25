using System;
using System.Reflection.Emit;
using System.Threading;

namespace Unity.Mvc.Wcf
{
    internal static class ILGeneratorHelpers
    {
        /// <summary>
        /// Loads the implicit "this" argument onto the stack.
        /// </summary>
        /// <param name="gen">The IL stream of the method being built.</param>
        internal static void LoadThis(this ILGenerator gen)
        {
            gen.Emit(OpCodes.Ldarg_0);
        }

        /// <summary>
        /// Intelligently uses the optimal OpCode to load a method argument onto the stack.
        /// </summary>
        /// <param name="gen">The IL stream of the method being built.</param>
        /// <param name="index">The index of the argument to load (excluding the implicit "this").</param>
        internal static void LoadArg(this ILGenerator gen, int index)
        {
            switch (index)
            {
                case 0:
                    gen.Emit(OpCodes.Ldarg_1);
                    break;
                case 1:
                    gen.Emit(OpCodes.Ldarg_2);
                    break;
                case 2:
                    gen.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    gen.Emit(OpCodes.Ldarg_S, (byte)(index + 1));
                    break;
            }
        }
    }

    /// <summary>
    /// Convenience wrapper around the ReaderWriterLockSlim class
    /// which enables managing locks with C#'s "using" statement.
    /// </summary>
    internal class ReaderWriterLockSlimWrapper : IDisposable
    {
        private ReaderWriterLockSlim locker;

        public ReaderWriterLockSlimWrapper() : this(LockRecursionPolicy.NoRecursion) { }
        public ReaderWriterLockSlimWrapper(LockRecursionPolicy recursionPolicy)
        {
            locker = new ReaderWriterLockSlim(recursionPolicy);
        }

        public IDisposable GetReadLock()
        {
            return new LockWrap(locker.EnterReadLock, locker.ExitReadLock);
        }

        public IDisposable GetWriteLock()
        {
            return new LockWrap(locker.EnterWriteLock, locker.ExitWriteLock);
        }

        public IDisposable GetUpgradeableReadLock()
        {
            return new LockWrap(locker.EnterUpgradeableReadLock, locker.ExitUpgradeableReadLock);
        }

        public void Dispose()
        {
            locker.Dispose();
        }

        private class LockWrap : IDisposable
        {
            private Action disposer;
            public LockWrap(Action enterer, Action disposer)
            {
                enterer();
                this.disposer = disposer;
            }

            public void Dispose()
            {
                disposer();
            }
        }
    }
}
