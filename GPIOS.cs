using System;
using System.Runtime.InteropServices;

namespace GPIOSimp;

/// <summary>
/// Provides access to GPIO functionality on Raspberry Pi devices.
/// </summary>
public class GPIOS : IDisposable
{
    /// <summary>
    /// Gets or sets the Raspberry Pi version.
    /// </summary>
    public int PiVersion { get; set; } = 3;

    private long GPIOPeriBase;
    private long GPIOBase;
    private const int PageSize = 4096;
    private const int BlockSize = 4096;

    private IntPtr gpioMap = IntPtr.Zero;
    private bool disposed = false;

    [DllImport("libc")]
    private static extern IntPtr mmap(
        IntPtr addr,
        long length,
        int prot,
        int flags,
        int fd,
        long offset
    );

    [DllImport("libc")]
    private static extern int munmap(IntPtr addr, long length);

    [DllImport("libc")]
    private static extern int open(string pathname, int flags);

    [DllImport("libc")]
    private static extern int close(int fd);

    /// <summary>
    /// Initializes a new instance of the GPIOS class.
    /// </summary>
    /// <param name="version">The Raspberry Pi version (1, 2, 3, 4, or 5).</param>
    /// <exception cref="Exception">Thrown when failing to open /dev/mem or map GPIO memory.</exception>
    /// <exception cref="ArgumentException">Thrown when an unsupported Raspberry Pi version is provided.</exception>
    public GPIOS(int version)
    {
        PiVersion = version;

        GPIOPeriBase = PiVersion switch
        {
            1 => 0x20000000,
            2 or 3 => 0x3F000000,
            4 => 0xFE000000,
            5 => 0x1f000d0000,
            _ => throw new ArgumentException("Unsupported Raspberry Pi version")
        };

        GPIOBase = GPIOPeriBase + 0x200000;

        int mem_fd = open("/dev/mem", 2); // O_RDWR
        if (mem_fd < 0)
        {
            throw new Exception("Failed to open /dev/mem");
        }

        gpioMap = mmap(IntPtr.Zero, BlockSize, 3, 1, mem_fd, GPIOBase);
        close(mem_fd);

        if (gpioMap.ToInt64() == -1)
        {
            throw new Exception("Failed to map GPIO memory");
        }
    }

    /// <summary>
    /// Sets the mode of a GPIO pin.
    /// </summary>
    /// <param name="pin">The GPIO pin number (0-53).</param>
    /// <param name="mode">The mode to set for the pin.</param>
    /// <exception cref="ArgumentException">Thrown when an invalid GPIO pin number is provided.</exception>
    public void SetPinMode(int pin, PinMode mode)
    {
        if (pin < 0 || pin > 53)
            throw new ArgumentException("Invalid GPIO pin number");

        int fsel = pin / 10;
        int shift = (pin % 10) * 3;

        unsafe
        {
            int* gpfsel = (int*)gpioMap + fsel;
            *gpfsel = (*gpfsel & ~(7 << shift)) | ((int)mode << shift);
        }
    }

    /// <summary>
    /// Writes a value to a GPIO pin.
    /// </summary>
    /// <param name="pin">The GPIO pin number (0-53).</param>
    /// <param name="value">The value to write (true for high, false for low).</param>
    /// <exception cref="ArgumentException">Thrown when an invalid GPIO pin number is provided.</exception>
    public void Write(int pin, bool value)
    {
        if (pin < 0 || pin > 53)
            throw new ArgumentException("Invalid GPIO pin number");

        int offset = value ? 7 : 10; // GPSET0 : GPCLR0
        int mask = 1 << (pin & 31);

        unsafe
        {
            *(int*)((byte*)gpioMap + offset * 4) = mask;
        }
    }

    /// <summary>
    /// Reads the current value of a GPIO pin.
    /// </summary>
    /// <param name="pin">The GPIO pin number (0-53).</param>
    /// <returns>The current value of the pin (true for high, false for low).</returns>
    /// <exception cref="ArgumentException">Thrown when an invalid GPIO pin number is provided.</exception>
    public bool Read(int pin)
    {
        if (pin < 0 || pin > 53)
            throw new ArgumentException("Invalid GPIO pin number");

        int offset = 13; // GPLEV0
        int mask = 1 << (pin & 31);

        unsafe
        {
            return (*(int*)((byte*)gpioMap + offset * 4) & mask) != 0;
        }
    }

    /// <summary>
    /// Disposes of the GPIOS instance, releasing unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the GPIOS instance, releasing unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Dispose managed resources if any
            }

            if (gpioMap != IntPtr.Zero)
            {
                munmap(gpioMap, BlockSize);
                gpioMap = IntPtr.Zero;
            }

            disposed = true;
        }
    }

    /// <summary>
    /// Finalizer for the GPIOS class.
    /// </summary>
    ~GPIOS()
    {
        Dispose(false);
    }
}

/// <summary>
/// Defines the possible modes for a GPIO pin.
/// </summary>
public enum PinMode
{
    /// <summary>
    /// Sets the pin as an input.
    /// </summary>
    Input = 0,

    /// <summary>
    /// Sets the pin as an output.
    /// </summary>
    Output = 1,

    /// <summary>
    /// Sets the pin to alternate function 0.
    /// </summary>
    Alt0 = 4,

    /// <summary>
    /// Sets the pin to alternate function 1.
    /// </summary>
    Alt1 = 5,

    /// <summary>
    /// Sets the pin to alternate function 2.
    /// </summary>
    Alt2 = 6,

    /// <summary>
    /// Sets the pin to alternate function 3.
    /// </summary>
    Alt3 = 7,

    /// <summary>
    /// Sets the pin to alternate function 4.
    /// </summary>
    Alt4 = 3,

    /// <summary>
    /// Sets the pin to alternate function 5.
    /// </summary>
    Alt5 = 2
}
