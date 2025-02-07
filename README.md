# Keysight MSOX3104T Oscilloscope and 33500B Waveform Generator Control Interface

This project provides a C# interface to control the Keysight MSOX3104T Mixed Signal Oscilloscope. and KEYSIGHT 33500B Waveform Generator. It allows users to control the oscilloscope's settings and generate signals through a computer interface. 

# Test Tab
In the test tab, it tests the accuracy of the parameters produced by the waveform generator using the oscilloscope measurement functions. Completely automatic.

###  Features of Keysight MSOX3104T Oscilloscope Control Interface
- Horizontal axis control (timebase)
- Vertical axis control (voltage scales)
- Channel opening and closing
- Measurement funcions (Vpp, Vrms, frequency, period, mean (full screen cycle-to-cylce), amplitude, phase, duty cycle, pulse width, rise time, fall time, overshoot, preshoot, slew rate )

###  Features of Keysight 33500B Waveform Generator Control Interface
- Output On/Off 
- Creating sinus, square, triangle, ramp, pulse, noise, DC signals
- Adjusting the frequency, amplitude, offset, phase, duty cycle, symmetry, pulse width, leading edge, trailing edge, bandwidth parameters

###  Features of Test Screen
- Automatic test of the frequency, amplitude, duty cycle, pulse width parameters of the waveform generator
- It can tests both for CH1 and CH2 
- Provides results and total test time

### Hardware
- Keysight MSOX3104T Mixed Signal Oscilloscope
- USB connection between PC and oscilloscope
- Keysight 33500B Waveform Generator
- LAN connection between PC and waveform generator

### Software
- Windows 10 or later
- .NET Framework 4.8
- Keysight IO Libraries Suite (includes VISA)
- Keysight Connection Expert

