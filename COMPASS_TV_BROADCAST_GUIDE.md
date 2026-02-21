# TV Broadcast (Compass) Integration Guide

To broadcast the `DisplayApp` screen to an IPTV system or Compass software, you have several standard approaches depending on the capabilities of the target system. The `DisplayApp` is a standard Windows WPF application running in an edge-to-edge full-screen borderless window. It does not actively broadcast a video stream over the network itself. 

To bridge this gap, you can use one of the following methods to capture and stream the display output:

### 1. Hardware Video Encoder (HDMI to IP / H.264 Streamer)
**Best for:** Reliability, no performance impact on the PC running `DisplayApp`.
**How it works:** 
- Connect a secondary HDMI output from the PC running `DisplayApp` to an HDMI-to-IP hardware encoder.
- The encoder captures the screen and broadcasts it as an RTSP, RTMP, or UDP multicast stream.
- Your Compass software or IP TV receivers can tune into this stream directly.

### 2. OBS Studio with NDI or Virtual Camera
**Best for:** Software-only setups, highly configurable.
**How it works:**
- Install OBS Studio on the PC running `DisplayApp`.
- Set up a "Display Capture" or "Window Capture" source in OBS pointing to the `DisplayApp` window.
- Install the **OBS NDI Plugin** to output the Canvas as an NDI stream over your local network.
- Ensure Compass software supports NDI receiving, or use an NDI-to-HDMI decoder at the TV ends.

### 3. Desktop Streaming Software (VLC / FFmpeg)
**Best for:** Free software-based RTSP/UDP streaming.
**How it works:**
- Run a background script using FFmpeg with `gdigrab` to capture the desktop and transcode it to a network stream.
- Example command: `ffmpeg -f gdigrab -framerate 30 -i desktop -c:v libx264 -preset ultrafast -tune zerolatency -f mpegts udp://[MULTICAST_IP]:[PORT]`
- Compass software can then connect to this UDP/RTSP stream as a source.

### 4. Direct Screen Mirroring / Casting (Miracast / Google Cast)
**Best for:** Simple setups with smart TVs.
**How it works:**
- If the IP TVs are smart TVs that support Miracast or Google Cast, you can use Windows' built-in "Project" feature (Win + P -> Connect to a wireless display) to duplicate the screen directly to the target TV. Note that this typically only works for a single target display unless using specialized multicast software.

### Recommendation
For an enterprise environment connecting to **Compass**, **Hardware Video Encoding (Method 1)** is strongly recommended as it guarantees 24/7 uptime without loading the display PC. If a software-only approach is strictly required and Compass supports it, **NDI via OBS (Method 2)** provides the lowest latency and highest quality over a local gigabit network.
