using System.Runtime.CompilerServices;
using Drboum.Utilities.Runtime;
using JetBrains.Annotations;

namespace GameProject
{
    public interface IState<T>
    {
        public T CurrentState {
            get;
            set;
        }

        public void ReenterState(T defaultState = default);

        public bool HasChanged {
            get;
        }

        [Pure]
        public bool HasEnterState(T state);

        [Pure]
        public bool HasExitState(T state);
    }

    public static class StateExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsBitMask(this byte x, byte y)
        {
            return (x & y) != 0;
        }
    }

    public struct ByteState : IState<byte>
    {
        public byte PreviousState;
        internal byte _currentState;

        public byte CurrentState {
            get => _currentState;
            set {
                PreviousState = _currentState;
                _currentState = value;
            }
        }

        public void ReenterState(byte defaultState = 0)
        {
            PreviousState = defaultState;
        }

        public bool HasChanged => _currentState != PreviousState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public bool HasEnterState(byte state)
        {
            return CurrentState.ContainsBitMask(state) && !PreviousState.ContainsBitMask(state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public bool HasExitState(byte state)
        {
            return !CurrentState.ContainsBitMask(state) && PreviousState.ContainsBitMask(state);
        }
    }

    public struct UIntState : IState<uint>
    {
        public uint PreviousState;
        internal uint _currentState;

        public uint CurrentState {
            get => _currentState;
            set {
                PreviousState = _currentState;
                _currentState = value;
            }
        }

        public void ReenterState(uint defaultState = 0)
        {
            PreviousState = defaultState;
        }

        public bool HasChanged => _currentState != PreviousState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public bool HasEnterState(uint state)
        {
            return CurrentState.ContainsBitMask(state) && !PreviousState.ContainsBitMask(state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public bool HasExitState(uint state)
        {
            return !CurrentState.ContainsBitMask(state) && PreviousState.ContainsBitMask(state);
        }
    }
}