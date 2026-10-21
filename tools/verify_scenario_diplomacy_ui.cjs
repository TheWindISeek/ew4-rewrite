'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const root = path.resolve(__dirname, '..');
const context = vm.createContext({});
vm.runInContext(fs.readFileSync(path.join(root, 'rewrite/Ew4.Web/wwwroot/battlefield.js'), 'utf8'), context);
const ui = context.Ew4Battlefield;
let checks = 0;
function check(actual, expected) { assert.equal(actual, expected); checks++; }
for (const camp of ['player-camp', 'other-camp']) {
  for (const [relation, color, attackable] of [['allied', 'blue', false], ['neutral', 'black', true], ['hostile', 'red', true]]) {
    const state = {playerFaction:'player', factions:[{id:'player',camp:'player-camp'}, {id:'target',camp,relation}],
      cells:[{owner:'target'}], units:[]};
    check(ui.badgeColor(state, 'target'), color);
    check(ui.selectionHostile(state, 0), attackable);
    state.cells[0].owner = 'player';
    state.units = [{owner:'target',cell:0}];
    check(ui.selectionHostile(state, 0), attackable);
  }
}
for (const [camp, neutral, color, attackable] of [
  ['player-camp', false, 'blue', false], ['other-camp', false, 'red', true], ['neutral-camp', true, 'black', true]
]) {
  const state = {playerFaction:'player', factions:[{id:'player',camp:'player-camp'}, {id:'target',camp,neutral}],
    cells:[{owner:'target'}], units:[]};
  check(ui.badgeColor(state, 'target'), color);
  check(ui.selectionHostile(state, 0), attackable);
}
const report = {checks,errors:0};
const directory = path.join(root, 'rewrite/reports/scenario-recurrence-diplomacy-20261021');
fs.mkdirSync(directory,{recursive:true});
fs.writeFileSync(path.join(directory,'ui.json'), JSON.stringify(report));
console.log(JSON.stringify(report));
