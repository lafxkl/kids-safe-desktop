#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""把 frames/ 序列 + music.wav 合成 MP4，并抽一张封面图。"""
import glob, os, subprocess, sys
import imageio_ffmpeg

ROOT = os.path.dirname(os.path.abspath(__file__))
FF = imageio_ffmpeg.get_ffmpeg_exe()
frames_dir = os.path.join(ROOT, sys.argv[1] if len(sys.argv) > 1 else "frames")
name = sys.argv[2] if len(sys.argv) > 2 else "儿童安全桌面-宣传片-1080p"
frames = sorted(glob.glob(os.path.join(frames_dir, "f*.jpg")))
assert frames, "没有帧"
print("帧数", len(frames))

out = os.path.join(ROOT, name + ".mp4")
cmd = [FF, "-y", "-framerate", "30", "-i", os.path.join(frames_dir, "f%05d.jpg"),
       "-i", os.path.join(ROOT, "music.wav"),
       "-c:v", "libx264", "-preset", "slow", "-crf", "18",
       "-vf", "scale=in_range=full:out_range=tv,format=yuv420p",
       "-color_range", "tv", "-colorspace", "bt709", "-color_primaries", "bt709",
       "-color_trc", "bt709", "-profile:v", "high", "-level", "4.1", "-g", "60",
       "-c:a", "aac", "-b:a", "192k", "-shortest",
       "-movflags", "+faststart", out]
subprocess.run(cmd, check=True)
print("输出", out, round(os.path.getsize(out) / 1048576, 1), "MB")

# 封面：第 6.4 秒
poster = os.path.join(ROOT, os.path.splitext(name)[0] + "-封面.jpg")
subprocess.run([FF, "-y", "-ss", "6.4", "-i", out, "-frames:v", "1", "-update", "1",
                "-q:v", "2", poster], check=True)
print("封面", poster)
