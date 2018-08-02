// RUN: %dafny /compile:0 /dprint:"%t.dprint" /autoTriggers:1 /printTooltips "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// This is a regression test: OOB errors for matrices used to be reported on the
// quantifier that introduced the variables that constituted the invalid indices.

// WISH: It would be even better to report the error on the variables inside the
// array instead of the array itself.

method M(m: array2<int>)
  requires m != null
  ensures forall i, j :: m[i, j] == 0
{ }
