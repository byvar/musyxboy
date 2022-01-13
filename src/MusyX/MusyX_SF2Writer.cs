using BinarySerializer;
using BinarySerializer.Audio;
using BinarySerializer.Audio.RIFF;
using BinarySerializer.Audio.SF2;
using BinarySerializer.GBA.Audio.MusyX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusyXBoy {
	public class MusyX_SF2Writer {
		public MusyX_Song Song { get; set; }
		public MusyX_SF2Writer(MusyX_Song song) {
			Song = song;
		}

		public void Write(string outPath) {
			var info = new RIFF_Chunk_List() {
				Type = "INFO",
			}.CreateChunk();
			var sdta = new RIFF_Chunk_List() {
				Type = "sdta",
			}.CreateChunk();
			var pdta = new RIFF_Chunk_List() {
				Type = "pdta",
			}.CreateChunk();
			var sf2 = new RIFF_Chunk_RIFF() {
				Type = "sfbk",
				Chunks = new RIFF_Chunk[] { info, sdta, pdta },
			}.CreateChunk();
		}

	}
}
