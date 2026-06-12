/**
 * Regenerates backend/Data/demo-products.json from the planned demo menu structure.
 * Preserves the original HTML-menu products (19 items) and fills categories to match
 * frontend-admin categoryGroups.ts product counts.
 */
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outPath = path.join(root, 'backend', 'Data', 'demo-products.json');

const categories = [
  { name: 'Salate', description: 'Alle Salate werden mit einem Dressing nach Wahl zubereitet.', sortOrder: 1 },
  { name: 'Stangerl', description: 'Alle Stangerl werden mit Tomaten, Käse und Oregano zubereitet.', sortOrder: 2 },
  { name: 'Baguettes', description: 'Alle Baguettes werden mit Tomaten, Käse und Oregano zubereitet.', sortOrder: 3 },
  { name: 'Calzone', description: 'Alle Calzonen werden mit Tomaten, Käse und Oregano zubereitet.', sortOrder: 4 },
  { name: 'Pizza-mittel', description: 'Ø 36cm. Alle Pizzen werden mit Tomaten, Pizzakäse und Oregano zubereitet.', sortOrder: 5 },
  { name: 'Pizza-Partner', description: 'Ø 40cm. Alle Pizzen werden mit Tomaten, Pizzakäse und Oregano zubereitet.', sortOrder: 6 },
  { name: 'Familien-Pizza', description: 'Ø 50cm. Alle Pizzen werden mit Tomaten, Pizzakäse und Oregano zubereitet.', sortOrder: 7 },
  { name: 'Mexikanische-Pizza-mittel', description: 'Ø 36cm. Alle Pizzen werden mit Jalapenos und Tacosauce zubereitet.', sortOrder: 8 },
  { name: 'Mexikanische-Pizza-Partner', description: 'Ø 40cm. Alle Pizzen werden mit Jalapenos und Tacosauce zubereitet.', sortOrder: 9 },
  { name: 'Pasta', description: 'Alle Gerichte werden mit einer Nudelsorte nach Wahl zubereitet.', sortOrder: 10 },
  { name: 'Imbiss', description: 'Alle Gerichte werden mit einem Dip nach Wahl serviert.', sortOrder: 11 },
  { name: 'Burger', description: 'Alle Burger werden mit Pommes frites serviert.', sortOrder: 12 },
  { name: 'Kebap', description: 'Alle Gerichte werden mit Salat, Tomaten, Zwiebeln, Rotkraut und einer Sauce nach Wahl zubereitet.', sortOrder: 13 },
  { name: 'Desserts', description: '', sortOrder: 14 },
  { name: 'Alkoholfreie-Getrnke', description: '', sortOrder: 15 },
];

/** @type {Array<{name:string,description:string,price:number,taxRate:number,category:string}>} */
const products = [];

function add(product) {
  products.push(product);
}

function addMany(items) {
  for (const item of items) add(item);
}

// --- Original HTML menu products (authoritative) ---
addMany([
  { name: 'chefsalat', description: 'grüner Salat mit Kebapfleisch, Zwiebeln, Tomaten, Schafskäse, Gurken und Paprika, dazu ein Stangerlbrot', price: 9.5, taxRate: 10, category: 'Salate' },
  { name: 'thunfischsalat', description: 'grüner Salat mit Thunfisch, Tomaten, Gurken, Oliven und Zwiebeln, dazu ein Stangerlbrot', price: 9.0, taxRate: 10, category: 'Salate' },
  { name: 'salat capricciosa', description: 'grüner Salat mit Schinken, Ei, Tomaten und Gurken, dazu ein Stangerlbrot', price: 9.0, taxRate: 10, category: 'Salate' },
  { name: 'bauernsalat', description: 'grüner Salat mit Schafskäse, Oliven, Tomaten und Gurken, dazu ein Stangerlbrot', price: 9.0, taxRate: 10, category: 'Salate' },
  { name: 'spinat-stangerl', description: 'mit Schafskäse und Knoblauch', price: 9.0, taxRate: 10, category: 'Stangerl' },
  { name: 'thunfisch-stangerl', description: 'mit Zwiebeln und Knoblauch', price: 9.0, taxRate: 10, category: 'Stangerl' },
  { name: 'schinken-stangerl', description: 'mit Schinken und Mais', price: 9.0, taxRate: 10, category: 'Stangerl' },
  { name: 'pizza margherita', description: '', price: 9.8, taxRate: 10, category: 'Pizza-mittel' },
  { name: 'pizza al funghi', description: 'mit Champignons', price: 10.8, taxRate: 10, category: 'Pizza-mittel' },
  { name: 'pizza prosciutto', description: 'mit Schinken', price: 11.7, taxRate: 10, category: 'Pizza-mittel' },
  { name: 'pasta bolognese', description: 'mit Fleischsauce', price: 9.5, taxRate: 10, category: 'Pasta' },
  { name: 'pasta carbonara', description: 'mit Schinken, Speck und Sahnesauce', price: 9.5, taxRate: 10, category: 'Pasta' },
  { name: 'hamburger', description: 'mit 180g österreichischem Rindfleisch-Patty, Salat, Tomaten, Zwiebeln, Gurken und Haussauce', price: 9.9, taxRate: 10, category: 'Burger' },
  { name: 'cheeseburger', description: 'mit 180g österreichischem Rindfleisch-Patty, Cheddar, Salat, Tomaten, Zwiebeln, Gurken und Haussauce', price: 9.9, taxRate: 10, category: 'Burger' },
  { name: 'döner kebap', description: '', price: 5.4, taxRate: 10, category: 'Kebap' },
  { name: 'dürüm', description: '', price: 6.5, taxRate: 10, category: 'Kebap' },
  { name: 'schnitzel-teller', description: 'mit Pommes frites', price: 11.0, taxRate: 10, category: 'Imbiss' },
  { name: 'coca cola 0,33l', description: '', price: 3.05, taxRate: 20, category: 'Alkoholfreie-Getrnke' },
  { name: 'fanta 0,5l', description: '', price: 3.25, taxRate: 20, category: 'Alkoholfreie-Getrnke' },
]);

const existingNames = new Set(products.map((p) => `${p.category}::${p.name}`));

function ensure(category, name, description, price, taxRate = 10) {
  const key = `${category}::${name}`;
  if (existingNames.has(key)) return;
  existingNames.add(key);
  add({ name, description, price, taxRate, category });
}

// Salate (10)
for (const [name, desc, price] of [
  ['griechischer salat', 'mit Feta, Oliven und Peperoni', 8.5],
  ['caesar salat', 'mit Hähnchenstreifen und Parmesan', 9.2],
  ['gemischter salat', 'mit Dressing nach Wahl', 7.5],
  ['tomaten-mozzarella salat', 'mit Basilikum und Olivenöl', 8.0],
  ['puten-salat', 'mit Putenstreifen und Mais', 9.3],
  ['fitness-salat', 'mit Käse und Ei', 8.8],
]) ensure('Salate', name, desc, price);

// Stangerl (11)
for (const [name, desc, price] of [
  ['klassisch-stangerl', 'mit Knoblauchöl', 8.5],
  ['käse-stangerl', 'mit Mozzarella überbacken', 9.0],
  ['salami-stangerl', 'mit scharfer Salami', 9.2],
  ['mais-stangerl', 'mit Mais und Paprika', 9.0],
  ['kebap-stangerl', 'mit Kebapfleisch', 9.5],
  ['sardellen-stangerl', 'mit Sardellen und Kapern', 9.3],
  ['peperoni-stangerl', 'mit Peperoni', 9.1],
  ['vegetarisch-stangerl', 'mit Gemüse', 8.9],
]) ensure('Stangerl', name, desc, price);

// Baguettes (10)
for (const [name, desc, price] of [
  ['baguette schinken-käse', 'mit Salat und Sauce', 6.5],
  ['baguette thunfisch', 'mit Ei und Mais', 6.8],
  ['baguette vegetarisch', 'mit Gemüse und Mozzarella', 6.2],
  ['baguette hänchen', 'mit Curry-Dressing', 6.9],
  ['baguette kebap', 'mit Kebapfleisch', 7.2],
  ['baguette salami', 'mit Salami und Käse', 6.7],
  ['baguette tonno', 'mit Thunfisch und Zwiebeln', 6.6],
  ['baguette spezial', 'mit Schinken, Salami und Käse', 7.0],
  ['baguette spinat', 'mit Spinat und Schafskäse', 6.4],
  ['baguette mozza', 'mit Mozzarella und Tomaten', 6.3],
]) ensure('Baguettes', name, desc, price);

const pizzaBases = [
  ['margherita', '', 9.8],
  ['al funghi', 'mit Champignons', 10.8],
  ['prosciutto', 'mit Schinken', 11.7],
  ['salami', 'mit scharfer Salami', 11.2],
  ['napoli', 'mit Sardellen und Kapern', 11.0],
  ['capricciosa', 'mit Schinken, Artischocken und Oliven', 12.0],
  ['hawaii', 'mit Schinken und Ananas', 11.5],
  ['diavolo', 'scharf mit Peperoni', 11.8],
  ['tonno', 'mit Thunfisch und Zwiebeln', 12.1],
  ['quattro formaggi', 'mit vier Käsesorten', 12.4],
  ['regina', 'mit Schinken und Champignons', 12.0],
  ['vegetariana', 'mit Gemüse der Saison', 11.3],
  ['spinaci', 'mit Spinat und Knoblauch', 11.6],
  ['gorgonzola', 'mit Gorgonzola und Walnüssen', 12.2],
  ['mare', 'mit Meeresfrüchten', 13.5],
  ['rustica', 'mit Speck und Zwiebeln', 12.3],
  ['tirolese', 'mit Speck und Schafskäse', 12.5],
  ['calabrese', 'mit scharfer Salami und Peperoni', 12.1],
  ['americana', 'mit Mais und Paprika', 11.4],
  ['bolognese', 'mit Fleischsauce', 11.9],
  ['carciofi', 'mit Artischocken', 11.7],
  ['parmigiana', 'mit Auberginen und Parmesan', 12.0],
  ['siciliana', 'mit Kapern und Oliven', 11.8],
  ['frutti di mare', 'mit Meeresfrüchten', 13.8],
  ['romana', 'mit Schinken und Ei', 11.6],
];

for (const [slug, desc, base] of pizzaBases) {
  ensure('Pizza-mittel', `pizza ${slug}`, desc, base);
  ensure('Pizza-Partner', `pizza ${slug} partner`, desc, Math.round(base * 1.18 * 100) / 100);
}

ensure('Familien-Pizza', 'pizza margherita familien', 'Ø 50cm', 24.9);

const mexPizzas = [
  ['el diablo', 'scharf mit Jalapenos', 12.5],
  ['taco', 'mit Hackfleisch und Tacosauce', 12.8],
  ['jalapeno', 'extra scharf', 12.6],
  ['mexicana', 'mit Mais, Bohnen und Jalapenos', 12.9],
];
for (const [slug, desc, base] of mexPizzas) {
  ensure('Mexikanische-Pizza-mittel', `mexikanische pizza ${slug}`, desc, base);
  ensure('Mexikanische-Pizza-Partner', `mexikanische pizza ${slug} partner`, desc, Math.round(base * 1.18 * 100) / 100);
}

for (const [name, desc, price] of [
  ['calzone classica', 'Tomaten, Mozzarella, Schinken', 10.5],
  ['calzone speciale', 'Salami, Schinken, Champignons', 11.2],
  ['calzone vegetarisch', 'Gemüse und Mozzarella', 10.0],
  ['calzone hawaii', 'Schinken und Ananas', 11.0],
  ['calzone prosciutto', 'mit Schinken', 10.8],
  ['calzone funghi', 'mit Champignons', 10.6],
  ['calzone salami', 'mit Salami', 11.1],
  ['calzone spinaci', 'mit Spinat und Knoblauch', 10.7],
  ['calzone tonno', 'mit Thunfisch', 11.3],
  ['calzone diavolo', 'scharf mit Peperoni', 11.4],
  ['calzone quattro formaggi', 'mit vier Käsesorten', 11.8],
]) ensure('Calzone', name, desc, price);

for (const [name, desc, price] of [
  ['pasta arrabbiata', 'Tomaten-Chili-Sauce', 9.2],
  ['pasta lasagne', 'hausgemacht mit Béchamel', 11.5],
  ['pasta aglio olio', 'mit Knoblauch und Olivenöl', 8.9],
  ['pasta pesto', 'mit Basilikum-Pesto', 9.8],
]) ensure('Pasta', name, desc, price);

for (const [name, desc, price] of [
  ['chickenburger', 'mit Hähnchenfilet und Haussauce', 10.2],
  ['bbq burger', 'mit BBQ-Sauce und Bacon', 10.8],
  ['double cheeseburger', 'mit doppeltem Cheddar', 11.2],
  ['veggie burger', 'mit Gemüse-Patty', 9.8],
  ['chili cheeseburger', 'mit Jalapenos und Chili-Cheese', 10.5],
  ['bacon burger', 'mit Speck und Cheddar', 10.9],
  ['fish burger', 'mit Fischfilet', 9.7],
]) ensure('Burger', name, desc, price);

for (const [name, desc, price] of [
  ['kebap-teller', 'mit Pommes und Salat', 9.5],
  ['kebap überbacken', 'mit Käse überbacken', 8.9],
  ['kebap box', 'mit Pommes und Sauce', 8.2],
  ['yaprak sarma', 'mit Reis und Hackfleisch', 7.8],
  ['falafel-teller', 'mit Pommes und Hummus', 8.5],
  ['falafel dürüm', 'im Fladenbrot', 7.2],
  ['kebap pizza', 'Pizzabrot mit Kebapfleisch', 8.8],
  ['kebap salat', 'nur mit Salat', 7.5],
]) ensure('Kebap', name, desc, price);

for (const [name, desc, price] of [
  ['chicken nuggets', '6 Stück mit Pommes', 8.5],
  ['chicken wings', '8 Stück mit Dip', 9.2],
  ['pommes frites', 'große Portion', 4.5],
  ['würstel mit pommes', 'mit Ketchup und Senf', 7.8],
  ['corn dog', 'mit Pommes', 6.9],
  ['mozzarella sticks', '6 Stück mit Dip', 7.5],
  ['onion rings', 'mit Dip', 6.8],
  ['fischstäbchen', 'mit Pommes und Salat', 9.0],
  ['chicken strips', 'mit Pommes', 8.9],
  ['currywurst mit pommes', 'mit Currysauce', 8.2],
]) ensure('Imbiss', name, desc, price);

for (const [name, desc, price] of [
  ['mohr im hemd', 'warmer Schokoladenkuchen mit Vanillesauce', 5.8],
  ['palatschinken', 'mit Marillenmarmelade', 5.5],
  ['topfenstrudel', 'mit Vanillesauce', 5.9],
]) ensure('Desserts', name, desc, price);

const drinks = [
  ['sprite 0,33l', 3.05],
  ['sprite 0,5l', 3.25],
  ['mezzo mix 0,33l', 3.05],
  ['mezzo mix 0,5l', 3.25],
  ['almdudler 0,33l', 3.15],
  ['almdudler 0,5l', 3.35],
  ['eistee pfirsich 0,33l', 3.1],
  ['eistee zitrone 0,5l', 3.3],
  ['ayran 0,25l', 2.8],
  ['mineralwasser 0,33l', 2.5],
  ['mineralwasser 0,5l', 2.8],
  ['red bull 0,25l', 3.8],
  ['fanta 0,33l', 3.05],
  ['coca cola 0,5l', 3.25],
];
for (const [name, price] of drinks) {
  ensure('Alkoholfreie-Getrnke', name, '', price, 20);
}

const payload = { categories, products };
writeFileSync(outPath, `${JSON.stringify(payload, null, 2)}\n`, 'utf8');

const counts = products.reduce((acc, p) => {
  acc[p.category] = (acc[p.category] ?? 0) + 1;
  return acc;
}, /** @type {Record<string, number>} */ ({}));

console.log(`Wrote ${products.length} products to ${outPath}`);
console.log(JSON.stringify(counts, null, 2));
