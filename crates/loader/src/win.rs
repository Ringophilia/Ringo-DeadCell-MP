//! The handful of Win32 declarations the loader needs.
#![allow(non_snake_case)]

use std::ffi::c_void;

pub type Handle = *mut c_void;

pub const DLL_PROCESS_ATTACH: u32 = 1;
pub const MEM_COMMIT: u32 = 0x1000;
pub const MEM_RESERVE: u32 = 0x2000;
pub const PAGE_EXECUTE_READWRITE: u32 = 0x40;

#[link(name = "kernel32")]
extern "system" {
    pub fn GetModuleHandleW(name: *const u16) -> Handle;
    pub fn GetProcAddress(module: Handle, name: *const u8) -> *const u8;
    pub fn VirtualAlloc(addr: *mut c_void, size: usize, kind: u32, protect: u32) -> *mut c_void;
    pub fn VirtualProtect(addr: *mut c_void, size: usize, protect: u32, old: *mut u32) -> i32;
    pub fn FlushInstructionCache(process: Handle, addr: *const c_void, size: usize) -> i32;
    pub fn GetCurrentProcess() -> Handle;
}
