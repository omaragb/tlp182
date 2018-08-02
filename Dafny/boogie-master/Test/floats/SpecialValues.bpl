// RUN: %boogie -proverWarnings:1 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
procedure foo() returns (r : float24e8) {
  var d : float53e11;

  r := 0NaN24e8;
  r := 0nan24e8;
  assert !FEQ(r,0NaN24e8);  //NaN != NaN
  r := 0+oo24e8;
  assert r == 0e255f24e8;
  r := 0-oo24e8;
  assert r == -0e255f24e8;
  r := -5e255f24e8;

  d := 0NaN53e11;
  d := 0nan53e11;
  assert !DEQ(d,0NaN53e11);
  d := 0+oo53e11;
  assert DEQ(d,0e2047f53e11);
  d := 0-oo53e11;
  assert DEQ(d,-0e2047f53e11);
  d := -200e2000f53e11;

  d := 0+zero53e11;
  assert d == 0e0f53e11;
  d := 0-zero53e11;
  assert d == -0e0f53e11;
  assert DEQ(0+zero53e11, 0-zero53e11);

  return;
}

function {:builtin "fp.eq"} FEQ(float24e8,float24e8) returns (bool);
function {:builtin "fp.eq"} DEQ(float53e11,float53e11) returns (bool);
