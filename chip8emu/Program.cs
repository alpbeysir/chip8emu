using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        BinaryReader reader = new BinaryReader(new FileStream("program.bin", FileMode.Open));
        ushort[] data = new ushort[reader.BaseStream.Length];
        int index = 0;
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            data[index] = (ushort)((reader.ReadByte() << 8) | reader.ReadByte());
            index++;
        }
        reader.Close();

        Emu emu = new Emu(data);

        while (emu.PC < 2048)
        {
            emu.ExecuteNext();

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    string buffer = Convert.ToString(emu.screen[x, y], 2).PadLeft(8, '0');
                    buffer = buffer.Replace('0', ' ');
                    buffer = buffer.Replace('1', 'M');
                    Console.Write(buffer);
                    //Console.Write(" ");
                }
                Console.Write("\n");
            }

            Console.Write("Registers: ");
            for (int i = 0; i < emu.V.Length; i++)
            {
                Console.Write(emu.V[i].ToString("X2") + " ");
        
            }

            Console.ReadKey();
            Console.Clear();
        }
        Console.ReadKey();
    }
}

public class Emu
{
    public Random rand = new Random();
    public byte[] memory = new byte[4096];
    public byte[] V = new byte[16];
    public ushort PC = 512;
    public ushort I;
    public Stack<ushort> stack = new Stack<ushort>();
    public byte delayTimer;
    public byte soundTimer;
    public byte keyboard;

    public byte[,] screen = new byte[8, 32];

    public byte Vf 
    { 
        get { return V[15]; }
        set { V[15] = value; }  
    }
    public Emu(ushort[] _program)
    {
        for (int i = 0; i < _program.Length; i++)
        {
            SetMemory((ushort)(512 + i * 2), _program[i]);
        }
    }
    public byte GetByte(ushort addr)
    {
        return memory[addr];
    }
    public ushort GetShort(ushort addr)
    {
        Debug.WriteLine("Memory location " + addr.ToString("X4") + " accessed");
        return (ushort)(((ushort)memory[addr] << 8) + (ushort)memory[addr + 1]);
    }

    public void SetMemory(ushort addr, ushort data)
    {
        byte msb = (byte)(data >> 8);
        byte lsb = (byte)(data);
        memory[addr] = msb;
        memory[addr + 1] = lsb;
    }
    public void SetMemory(ushort addr, byte data)
    {
        memory[addr] = data;
    }
    public void ExecuteNext()
    {
        Opcode op = new Opcode(GetShort(PC));
        byte Vx = V[op.Second4];
        byte Vy = V[op.Third4];
        switch (op.First4)
        {
            case 0:
                if (op.Last4 == 0) screen = new byte[64, 32];
                else if (op.Last4 == 0xE) PC = stack.Pop();
                else Debug.WriteLine("Unsupported opcode!: " + op.data.ToString("X4"));
                break;
            case 1:
                PC = op.Last12;
                break;
            case 2:
                stack.Push(PC);
                PC = op.Last12;
                break;
            case 3:
                if (Vx == op.Last8) PC += 2;
                break;
            case 4:
                if (Vx != op.Last8) PC += 2;
                break;
            case 5:
                if (Vx == V[op.Third4]) PC += 2;
                break;
            case 6:
                Debug.WriteLine("Set V" + op.Second4 + " to " + op.Last8);
                V[op.Second4] = (byte)op.Last8;
                break;
            case 7:
                V[op.Second4] += (byte)op.Last8;
                break;
            case 8:
                switch (op.Last4)
                {
                    case 0:
                        Debug.WriteLine("Set V" + op.Second4 + " to the value of V" + op.Third4);
                        V[op.Second4] = Vy;
                        break;
                    case 1:
                        V[op.Second4] = (byte)(Vx | Vy);
                        break;
                    case 2:
                        V[op.Second4] = (byte)(Vx & Vy);
                        break;
                    case 3:
                        V[op.Second4] = (byte)(Vx ^ Vy);
                        break;
                    case 4:
                        Vf = Vx + Vy > 255 ? (byte)1 : (byte)0;
                        V[op.Second4] += Vy;
                        break;
                    case 5:
                        Vf = Vx - Vy < 0 ? (byte)1 : (byte)0;
                        V[op.Second4] -= Vy;
                        break;
                    case 6:
                        Vf = (byte)(Vx % 2);
                        V[op.Second4] >>= 1;
                        break;
                    case 7:
                        Vf = Vy - Vx < 0 ? (byte)1 : (byte)0;
                        V[op.Second4] = (byte)(Vy - Vx);
                        break;
                    case 14:
                        Vf = Vx > 127 ? (byte)1 : (byte)0;
                        V[op.Second4] <<= 1;
                        break;
                }
                break;
            case 9:
                if (Vx != Vy) PC += 2;
                break;
            case 0xA:
                Debug.WriteLine("Address pointer I set to " + op.Last12);
                I = op.Last12;
                break;
            case 0xB:
                Debug.WriteLine("Program counter set");
                PC = (ushort)(op.Last12+ V[0]);
                break;
            case 0xC:
                V[op.Second4] = (byte)(rand.Next(0, 255) & op.Last8);
                break;
            case 0xD:
                Draw(Vx, Vy, (byte)op.Last4);
                break;
            default:
                Debug.WriteLine("Unsupported opcode!: " + op.data.ToString("X4"));
                break;
        }
        PC += 2;
        string temp;
    }

    private void Draw(byte x, byte y, byte height)
    {
        Vf = 0;
        int majorX = (int)x / 8;
        for (int i = 0; i < height; i++)
        {
            int majorY = y + i;
            byte data = memory[I + i];
            byte dataLeft = (byte)(data >> x % 8);
            byte dataRight = (byte)(data << x % 8);
            //Debug.WriteLine("MajorX: " + majorX + " MajorY: " + majorY);
            screen[majorX, majorY] ^= dataLeft;
            screen[majorX + 1 > 7 ? 0 : majorX + 1, majorY] ^= dataRight;
        }
        Debug.WriteLine("Sprite drawn at " + x + ", " + y);
    }

    private class Opcode
    {
        public ushort data;

        public ushort First4 => (ushort)((data & 0xF000) >> 12);
        public ushort Second4 => (ushort)((data & 0x0F00) >> 8);
        public ushort Third4 => (ushort)((data & 0x00F0) >> 4);
        public ushort Last4 => (ushort)(data & 0x000F);
        public ushort Last8 => (ushort)(data & 0x00FF);
        public ushort Last12 => (ushort)(data & 0x0FFF);

        public Opcode(ushort opcode) 
        {
            data = opcode;
        }
    }
}