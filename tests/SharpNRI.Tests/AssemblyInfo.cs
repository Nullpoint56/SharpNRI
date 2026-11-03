// Tests run one at a time: see xunit.runner.json beside this file.
//
// NRI's D3D12 validation and Agility settings are process-global, the embedded validation layer
// reports through one callback, and a test that creates a device on a software adapter is already
// slow enough that interleaving two of them would hide which one produced a message.
