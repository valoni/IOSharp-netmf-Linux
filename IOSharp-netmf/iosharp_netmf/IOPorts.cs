using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections;
using Linux.SPOT.Manager;

namespace Linux.SPOT.Hardware
{

    public delegate void NativeEventHandler(uint data1, uint data2, DateTime time);

    //--//

    public class NativeEventDispatcher : IDisposable
    {
        protected NativeEventHandler m_threadSpawn = null;
        protected NativeEventHandler m_callbacks = null;
        protected bool m_disposed = false;
        private object m_NativeEventDispatcher;

        //--//
        public NativeEventDispatcher() { }

        public NativeEventDispatcher(string strDriverName, ulong drvData)
        {
        }

        public virtual void EnableInterrupt() { }

        public virtual void DisableInterrupt() { }

        protected virtual void Dispose(bool disposing) { }

        //--//

        ~NativeEventDispatcher()
        {
            Dispose(false);
        }

        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public virtual void Dispose()
        {
            if (!m_disposed)
            {
                Dispose(true);

                GC.SuppressFinalize(this);

                m_disposed = true;
            }
        }

        public event NativeEventHandler OnInterrupt
        {
            [MethodImplAttribute(MethodImplOptions.Synchronized)]
            add
            {
                if (m_disposed)
                {
                    throw new ObjectDisposedException("");
                }

                NativeEventHandler callbacksOld = m_callbacks;
                NativeEventHandler callbacksNew = (NativeEventHandler)Delegate.Combine(callbacksOld, value);

                try
                {
                    m_callbacks = callbacksNew;

                    if (callbacksNew != null)
                    {
                        if (callbacksOld == null)
                        {
                            EnableInterrupt();
                        }

                        if (callbacksNew.Equals(value) == false)
                        {
                            callbacksNew = new NativeEventHandler(this.MultiCastCase);
                        }
                    }

                    m_threadSpawn = callbacksNew;
                }
                catch
                {
                    m_callbacks = callbacksOld;

                    if (callbacksOld == null)
                    {
                        DisableInterrupt();
                    }

                    throw;
                }
            }

            [MethodImplAttribute(MethodImplOptions.Synchronized)]
            remove
            {
                if (m_disposed)
                {
                    throw new ObjectDisposedException("");
                }

                NativeEventHandler callbacksOld = m_callbacks;
                NativeEventHandler callbacksNew = (NativeEventHandler)Delegate.Remove(callbacksOld, value);

                try
                {
                    m_callbacks = (NativeEventHandler)callbacksNew;

                    if (callbacksNew == null && callbacksOld != null)
                    {
                        DisableInterrupt();
                    }
                }
                catch
                {
                    m_callbacks = callbacksOld;

                    throw;
                }
            }
        }

        private void MultiCastCase(uint port, uint state, DateTime time)
        {
            NativeEventHandler callbacks = m_callbacks;

            if (callbacks != null)
            {
                callbacks(port, state, time);
            }
        }
    }

    //--//

    public class Port : NativeEventDispatcher
    {
        public enum ResistorMode
        {
            Disabled = 0,
            PullDown = 1,
            PullUp = 2,
        }

        public enum InterruptMode
        {
            InterruptNone = 0,
            InterruptEdgeLow = 1,
            InterruptEdgeHigh = 2,
            InterruptEdgeBoth = 3,
            InterruptEdgeLevelHigh = 4,
            InterruptEdgeLevelLow = 5,
        }

        //--//

        private InterruptMode m_interruptMode;
        private ResistorMode m_resistorMode;
        private uint m_portId;
        private uint m_flags;
        private bool m_glitchFilterEnable;
        private bool m_initialState;
        //--//

        protected Port(Cpu.Pin portId, bool glitchFilter, ResistorMode resistor, InterruptMode interruptMode)
        {
            throw new NotImplementedException();
        }

        protected Port(Cpu.Pin portId, bool initialState) {
            GPIOManager.Instance.Export(portId);
        }

        protected Port(Cpu.Pin portId, bool initialState, bool glitchFilter, ResistorMode resistor) {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            Console.WriteLine("Disposing({0})", this.Id);
            GPIOManager.Instance.Unexport(this.Id);
        }

        public bool Read()
        {
            Console.WriteLine("Reading");
            return GPIOManager.Instance.Read(this.Id);
        }

        public Cpu.Pin Id { get; set; }

        static public bool ReservePin(Cpu.Pin pin, bool fReserve) { return false; }
    }

    //--//

    public class InputPort : Port
    {
        public InputPort(Cpu.Pin portId, bool glitchFilter, ResistorMode resistor)
            : base(portId, glitchFilter, resistor, InterruptMode.InterruptNone)
        {
            GPIOManager.Instance.SetPortType(portId, PortType.INPUT);
        }

        protected InputPort(Cpu.Pin portId, bool glitchFilter, ResistorMode resistor, InterruptMode interruptMode)
            : base(portId, glitchFilter, resistor, interruptMode)
        {
            throw new NotImplementedException();
        }

        protected InputPort(Cpu.Pin portId, bool initialState, bool glitchFilter, ResistorMode resistor)
            : base(portId, initialState, glitchFilter, resistor)
        {
            throw new NotImplementedException();
        }

        public ResistorMode Resistor { get; set; }

        public bool GlitchFilter { get; set; }

    }

    //--//

    public class OutputPort : Port
    {
        public OutputPort(Cpu.Pin portId, bool initialState)
            : base(portId, initialState)
        {
            this.Id = portId;
            GPIOManager.Instance.SetPortType(portId, PortType.OUTPUT);
        }

        protected OutputPort(Cpu.Pin portId, bool initialState, bool glitchFilter, ResistorMode resistor)
            : base(portId, initialState, glitchFilter, resistor)
        {
            throw new NotImplementedException();
        }

        public void Write(bool state){
            Console.WriteLine("Write");
            GPIOManager.Instance.Write(this.Id, state);
        }

        public bool InitialState { get; set; }

    }

    //--//

    public sealed class TristatePort : OutputPort
    {
        public TristatePort(Cpu.Pin portId, bool initialState, bool glitchFilter, ResistorMode resistor)
            : base(portId, initialState, glitchFilter, resistor)
        {
            GPIOManager.Instance.SetPortType(portId, PortType.TRISTATE);
        }

        public bool Active { get; set; }

        public ResistorMode Resistor { get; set; }

        public bool GlitchFilter { get; set; }
    }

    //--//

    public sealed class InterruptPort : InputPort
    {
        //--//

        public InterruptPort(Cpu.Pin portId, bool glitchFilter, ResistorMode resistor, InterruptMode interrupt)
            : base(portId, glitchFilter, resistor, interrupt)
        {
            m_threadSpawn = null;
            m_callbacks = null;
            GPIOManager.Instance.SetPortType(portId, PortType.INTERRUPT);
        }

        public void ClearInterrupt() { }

        public InterruptMode Interrupt { get; set; }

        public override void EnableInterrupt() { }

        public override void DisableInterrupt(){}

    }
}

