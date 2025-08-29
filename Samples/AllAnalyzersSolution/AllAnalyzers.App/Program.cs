using System;
using System.Threading.Tasks;

// Top-level program

// MNA0001: async void method (1)
async void Fire() { await Task.Delay(1); }
Fire();

// MNA0002: empty catch (1)
try { throw new Exception("boom"); } catch { }

// MNA0003A: missing required prefix -> "[APP]" is required by .editorconfig
Console.WriteLine("Started");

// MNA0003: general rule on Console.WriteLine even when prefixed
Console.WriteLine("[APP] Started");

// MNA0004: weak names (2)
int a = 0, b1 = 1;
_ = a + b1;
