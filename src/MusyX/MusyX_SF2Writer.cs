using BinarySerializer;
using BinarySerializer.Audio;
using BinarySerializer.Audio.RIFF;
using BinarySerializer.Audio.SF2;
using BinarySerializer.Audio.GBA.MusyX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusyXBoy {
	public class MusyX_SF2Writer {
		public MusyX_File File { get; set; }
		public MusyX_SongGroup SongGroup { get; set; }
		public MusyX_SF2Writer(MusyX_File file, MusyX_SongGroup songGroup) {
			File = file;
			SongGroup = songGroup;
		}

		public void Write(string outPath) {
			var sampleData = new RIFF_Chunk_SF2_SampleData();
			var presetHeaders = new List<RIFF_Chunk_SF2_PresetHeaders.PresetHeader>();
			var presetBags = new List<SF2_BagEntry>();
			var soundList = SongGroup.SoundList;
			ushort bank = 0;
			ushort preset_bagIndex = 0;
			ushort preset_genIndex = 0;
			ushort preset_modIndex = 0;
			for (int i = 0; i < soundList.Length; i++) {
				var item = soundList[i];
				if (item.MaxVoices == 0) continue;
				var id = item.ObjectID;
				var macro = File.InstrumentTable?.Value?.Macros[id];
				var presetHeader = new RIFF_Chunk_SF2_PresetHeaders.PresetHeader() {
					Bank = bank,
					Preset = (ushort)i,
					Name = $"Preset {i}",
					BagIndex = preset_bagIndex
				};
				var presetBag = new SF2_BagEntry() {
					GeneratorIndex = preset_genIndex,
					ModulatorIndex = preset_modIndex,
				};

				// Increase indices
				preset_genIndex++;
				preset_bagIndex++;
				presetHeaders.Add(presetHeader);
				presetBags.Add(presetBag);
			}

			// Create top-level chunks
			var info = new RIFF_Chunk_List() {
				Type = "INFO",
				Chunks = new RIFF_Chunk[] {
					 new RIFF_Chunk_SF2_Info_VersionTag() {
						 Major = 2,
						 Minor = 1
					 }.CreateChunk(),
					 new RIFF_Chunk_SF2_Info_BankName() {
						 SoundFontBankName = "MusyXBoy Export"
					 }.CreateChunk(),
					 new RIFF_Chunk_SF2_Info_SoundEngine() {
						 SoundEngineName = "E-mu 10K2"
					 }.CreateChunk()
				}
			}.CreateChunk();
			var sdta = new RIFF_Chunk_List() {
				Type = "sdta",
				Chunks = new RIFF_Chunk[] {
					sampleData.CreateChunk(),
				}
			}.CreateChunk();
			var pdta = new RIFF_Chunk_List() {
				Type = "pdta",
				Chunks = new RIFF_Chunk[] {
					// Presets
					new RIFF_Chunk_SF2_PresetHeaders() {
						Headers = presetHeaders.ToArray()
					}.CreateChunk(),
					new RIFF_Chunk_SF2_PresetBag() {
						Entries = presetBags.ToArray()
					}.CreateChunk(),
					new RIFF_Chunk_SF2_PresetModulatorList() {
					}.CreateChunk(),
					new RIFF_Chunk_SF2_PresetGeneratorList() {
					}.CreateChunk(),

					// Instruments
					new RIFF_Chunk_SF2_InstrumentHeaders() {
					}.CreateChunk(),
					new RIFF_Chunk_SF2_InstrumentBag() {
					}.CreateChunk(),
					new RIFF_Chunk_SF2_InstrumentModulatorList() {
					}.CreateChunk(),
					new RIFF_Chunk_SF2_InstrumentGeneratorList() {
					}.CreateChunk(),

					// Samples
					new RIFF_Chunk_SF2_SampleHeaders() {
					}.CreateChunk(),
				}
			}.CreateChunk();

			// Create SF2
			var sf2 = new RIFF_Chunk_RIFF() {
				Type = "sfbk",
				Chunks = new RIFF_Chunk[] { info, sdta, pdta },
			}.CreateChunk();
		}

	}
}
