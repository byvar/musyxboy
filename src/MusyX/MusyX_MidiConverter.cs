using BinarySerializer;
using BinarySerializer.GBA.Audio.MusyX;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusyXBoy {
	public class MusyX_MidiConverter {
		public MusyX_Song Song { get; set; }
		public TemporaryTrack[] Tracks { get; set; }
		public MusyX_MidiConverter(MusyX_Song song) {
			Song = song;
			Tracks = new TemporaryTrack[17];
			for (int i = 0; i < Tracks.Length; i++) {
				Tracks[i] = ConvertTrackToTemporaryFormat(song, song.Tracks[i]);
			}
		}

		public void Write(string outPath) {
			var s = new Sequence(96); // MIDI->MusyX converter multiplies all times by (96.0/original_division)
			s.Format = 1; // Synchronous tracks
			for (int i = 0; i < 16; i++) {
				if(Tracks[i] != null)
					s.Add(ConvertTemporaryToMidi(Tracks[i], i));
			}
			// This plugin doesn't overwrite files
			Directory.CreateDirectory(Path.GetDirectoryName(outPath));
			if (File.Exists(outPath)) {
				File.Delete(outPath);
			}
			s.Save(outPath);
		}

		public Track ConvertTemporaryToMidi(TemporaryTrack track, int channel) {
			Track t = new Track();

			// TODO: Add loop points, add tempo change events

			// Set BPM
			TempoChangeBuilder b = new TempoChangeBuilder();
			b.Tempo = 60000000 / Song.BPM;
			b.Build();
			t.Insert(0, b.Result);

			foreach (var e in track.Entries) {
				var msg = e.Message;
				if (msg != null) {
					if(msg.IsEnd) continue;
					if (msg.ProgramChange) {
						ChannelMessageBuilder builder = new ChannelMessageBuilder();
						builder.Command = ChannelCommand.ProgramChange;
						builder.MidiChannel = channel;
						builder.Data1 = msg.Patch;
						builder.Build();
						t.Insert((int)e.TotalTime, builder.Result);
					}
					if (msg.Note != 0) {
						ChannelMessageBuilder builder = new ChannelMessageBuilder();
						builder.Command = ChannelCommand.NoteOn;
						builder.MidiChannel = channel;
						builder.Data1 = msg.Note;
						builder.Data2 = msg.Velocity;
						builder.Build();
						t.Insert((int)e.TotalTime, builder.Result);

						builder.Command = ChannelCommand.NoteOff;
						builder.Data2 = 127;
						builder.Build();
						t.Insert((int)e.TotalTime + msg.SustainTime, builder.Result);
					}
				}
			}

			return t;
		}

		public TemporaryTrack ConvertTrackToTemporaryFormat(MusyX_Song song, MusyX_Track mtr) {
			if(mtr == null) return null;
			var track = new TemporaryTrack();
			// Loop
			if (mtr.Entries.Last().PatternIndex == -2) {
				track.LoopTime = (uint)(mtr.Entries[mtr.StartLoopEntryIndex].Time + mtr.StartLoopTime);
			}
			uint curTrackTime = 0;
			uint curTime = 0;
			foreach (var e in mtr.Entries) {
				/*if (curTrackTime+e.Time < curTime) {
					throw new BinarySerializableException(e, $"Track time: {e.Time} - current time: {curTime}");
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
		public class TemporaryTrack {
			public List<TrackEntry> Entries { get; set; } = new List<TrackEntry>();
			public uint? LoopTime { get; set; }
		}
	}
}
