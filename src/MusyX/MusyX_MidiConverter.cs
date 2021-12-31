using BinarySerializer;
using BinarySerializer.GBA.Audio.MusyX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusyXBoy {
	public class MusyX_MidiConverter {
		public MusyX_Song Song { get; set; }
		public Track[] Tracks { get; set; }
		public MusyX_MidiConverter(MusyX_Song song) {
			Song = song;
			Tracks = new Track[17];
			for (int i = 0; i < Tracks.Length; i++) {
				Tracks[i] = ConvertTrack(song, song.Tracks[i]);
			}
		}

		public Track ConvertTrack(MusyX_Song song, MusyX_Track mtr) {
			if(mtr == null) return null;
			var track = new Track();
			// Loop
			if (mtr.Entries.Last().PatternIndex == -2) {
				track.LoopTime = (uint)(mtr.Entries[mtr.StartLoopEntryIndex].Time + mtr.StartLoopTime);
			}
			uint curTrackTime = 0;
			uint curTime = 0;
			foreach (var e in mtr.Entries) {
				if (curTrackTime+e.Time < curTime) {
					throw new BinarySerializableException(e, $"Track time: {e.Time} - current time: {curTime}");
				} /*else if (curTime != 0) {
					Console.WriteLine($"Track time: {e.Time} - current time: {curTime}");
				}*/
				curTrackTime += (uint)e.Time;
				curTime = curTrackTime;
				if (e.PatternIndex < 0) {
					track.Entries.Add(new TrackEntry(curTime, null));
				} else {
					var pat = song.Patterns[e.PatternIndex].Value;
					foreach (var msg in pat.Messages) {
						if(msg.IsEnd) continue;
						curTime += msg.Time;
						track.Entries.Add(new TrackEntry(curTime, msg));
					}
				}
			}

			return track;
		}

		public class TrackEntry {
			public uint TotalTime { get; set; }
			public MusyX_Message Message { get; set; }

			public TrackEntry(uint time, MusyX_Message msg) {
				TotalTime = time;
				Message = msg;
			}
		}
		public class Track {
			public List<TrackEntry> Entries { get; set; } = new List<TrackEntry>();
			public uint? LoopTime { get; set; }
		}
	}
}
