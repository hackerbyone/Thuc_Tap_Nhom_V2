import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SOURCE_URL = 'https://thuysinhtim.vn/ca-canh-tep-canh';
const TARGET_COUNT = 100;
const CANDIDATE_COUNT = TARGET_COUNT + 30;
const SQL_SERVER = process.env.FISH_SQL_SERVER || 'MinhTri';
const SQL_DATABASE = process.env.FISH_SQL_DATABASE || 'FishDB';
const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const ROOT_DIR = path.resolve(SCRIPT_DIR, '..');
const ARTIFACT_DIR = path.join(ROOT_DIR, 'artifacts');
const JSON_PATH = path.join(ARTIFACT_DIR, 'thuysinhtim-products.json');
const SQL_PATH = path.join(ARTIFACT_DIR, 'thuysinhtim-products-import.sql');

const predatorKeywords = [
  'san moi', 'loc', 'khi', 'noc', 'chinh', 'frogfish', 'ca beo', 'bo cap',
  'hai long', 'pleco', 'rong', 'ho', 'la han', 'dia', 'hong ket',
  'moray', 'eel', 'catfish', 'pacu', 'gar', 'ray', 'cichlid', 'khung long'
];

const calmEnvironment = 'Be thuy sinh on dinh, nuoc sach, loc nhe va nhiet do phu hop cho ca/tep canh.';
const predatorEnvironment = 'Be rong, loc khoe, nap day chac va khong ghep voi cac loai qua nho.';
const calmCare = 'Cho an luong vua du, thay nuoc dinh ky va theo doi suc khoe truoc khi ghep dan.';
const predatorCare = 'Cho an theo khau phan, theo doi tinh lanh tho va tach rieng khi co dau hieu canh tranh.';

const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));

const decodeHtml = (value = '') => value
  .replace(/<[^>]*>/g, '')
  .replace(/&nbsp;/g, ' ')
  .replace(/&amp;/g, '&')
  .replace(/&quot;/g, '"')
  .replace(/&#39;/g, "'")
  .replace(/&apos;/g, "'")
  .replace(/&lt;/g, '<')
  .replace(/&gt;/g, '>')
  .replace(/&#(\d+);/g, (_, code) => String.fromCharCode(Number(code)))
  .replace(/\s+/g, ' ')
  .trim();

const normalizeText = (value = '') => value
  .normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '')
  .replace(/đ/g, 'd')
  .replace(/Đ/g, 'D')
  .toLowerCase();

const normalizeUrl = (url = '') => {
  if (!url) return '';
  if (url.startsWith('//')) return `https:${url}`;
  if (url.startsWith('/')) return `https://thuysinhtim.vn${url}`;
  return url;
};

const priceToNumber = (priceText = '') => {
  const digits = priceText.replace(/[^\d]/g, '');
  return digits ? Number(digits) : 0;
};

const sqlString = (value = '') => `N'${String(value).replace(/'/g, "''")}'`;

const isPredatorOrLarge = (name) => {
  const normalized = normalizeText(name);
  return predatorKeywords.some(keyword => normalized.includes(keyword));
};

const stockForIndex = (index) => 8 + ((index * 7) % 36);

const buildDescription = (name, categoryId) => {
  const type = categoryId === 2 ? 'ca canh size lon hoac co tap tinh san moi' : 'ca/tep canh phu hop be thuy sinh';
  return `${name} - du lieu mau tham khao tu danh muc Ca Canh, Tep Canh. San pham duoc them de bo sung kho demo cho website, phan mo ta da duoc viet moi. Nhom: ${type}.`;
};

const parseProducts = (html) => {
  const blocks = html.split('<div class="product-box">').slice(1);

  return blocks.map(block => {
    const anchor = block.match(/<h3 class="product-name">[\s\S]*?<a[^>]*href="([^"]+)"[^>]*title="([^"]+)"[\s\S]*?<\/a>/);
    const price = block.match(/<span class="price">\s*([^<]+)\s*<\/span>/);
    const image = block.match(/<img[^>]*data-lazyload="([^"]+)"[^>]*>/);

    if (!anchor || !price) return null;

    const name = decodeHtml(anchor[2]);
    const categoryId = isPredatorOrLarge(name) ? 2 : 1;

    return {
      name,
      sourceUrl: normalizeUrl(anchor[1]),
      price: priceToNumber(decodeHtml(price[1])),
      imageUrl: normalizeUrl(image?.[1] || ''),
      categoryId,
    };
  }).filter(Boolean);
};

const fetchPage = async (page) => {
  const url = page === 1 ? SOURCE_URL : `${SOURCE_URL}?page=${page}`;
  const response = await fetch(url, {
    headers: {
      'user-agent': 'Mozilla/5.0 (compatible; AquaVietDataSeeder/1.0)',
      'accept': 'text/html,application/xhtml+xml',
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch page ${page}: ${response.status}`);
  }

  return response.text();
};

const collectProducts = async () => {
  const byName = new Map();

  for (let page = 1; page <= 22 && byName.size < CANDIDATE_COUNT; page += 1) {
    const html = await fetchPage(page);
    const products = parseProducts(html);

    for (const product of products) {
      if (product.price <= 0 || byName.has(product.name)) continue;
      byName.set(product.name, product);
      if (byName.size >= CANDIDATE_COUNT) break;
    }

    await sleep(250);
  }

  return Array.from(byName.values()).slice(0, CANDIDATE_COUNT).map((product, index) => {
    const stock = stockForIndex(index + 1);
    return {
      ...product,
      stock,
      maleStock: 0,
      femaleStock: 0,
      description: buildDescription(product.name, product.categoryId),
      careInstructions: product.categoryId === 2 ? predatorCare : calmCare,
      environment: product.categoryId === 2 ? predatorEnvironment : calmEnvironment,
    };
  });
};

const buildSql = (products) => {
  const lines = [
    'SET ANSI_NULLS ON;',
    'SET QUOTED_IDENTIFIER ON;',
    'SET ANSI_PADDING ON;',
    'SET ANSI_WARNINGS ON;',
    'SET CONCAT_NULL_YIELDS_NULL ON;',
    'SET ARITHABORT ON;',
    'SET NUMERIC_ROUNDABORT OFF;',
    'SET NOCOUNT ON;',
    'BEGIN TRANSACTION;',
    'DECLARE @Before int = (SELECT COUNT(*) FROM Products);',
    `DECLARE @ExistingImported int = (SELECT COUNT(*) FROM Products WHERE Description LIKE '%du lieu mau tham khao tu danh muc Ca Canh, Tep Canh%');`,
    `DECLARE @Remaining int = CASE WHEN @ExistingImported >= ${TARGET_COUNT} THEN 0 ELSE ${TARGET_COUNT} - @ExistingImported END;`,
    'DECLARE @Inserted int = 0;',
  ];

  products.forEach(product => {
    lines.push(`
IF @Inserted < @Remaining AND NOT EXISTS (SELECT 1 FROM Products WHERE Name = ${sqlString(product.name)})
BEGIN
    INSERT INTO Products
        (Name, Price, Stock, ImageUrl, Description, CategoryId, CareInstructions, Environment, MaleStock, FemaleStock, PairPrice)
    VALUES
        (${sqlString(product.name)}, ${product.price}, ${product.stock}, ${sqlString(product.imageUrl)}, ${sqlString(product.description)}, ${product.categoryId}, ${sqlString(product.careInstructions)}, ${sqlString(product.environment)}, ${product.maleStock}, ${product.femaleStock}, NULL);
    SET @Inserted = @Inserted + @@ROWCOUNT;
END`);
  });

  lines.push(
    'DECLARE @After int = (SELECT COUNT(*) FROM Products);',
    'COMMIT;',
    "SELECT @Before AS BeforeCount, @After AS AfterCount, @ExistingImported AS ExistingImportedCount, @Remaining AS TargetRemaining, @Inserted AS InsertedCount;",
    "SELECT CategoryId, COUNT(*) AS ProductCount FROM Products GROUP BY CategoryId ORDER BY CategoryId;"
  );

  return `${lines.join('\n')}\n`;
};

const main = async () => {
  await fs.mkdir(ARTIFACT_DIR, { recursive: true });
  const products = await collectProducts();

  if (products.length < TARGET_COUNT) {
    throw new Error(`Only collected ${products.length} products`);
  }

  await fs.writeFile(JSON_PATH, JSON.stringify(products, null, 2), 'utf8');
  await fs.writeFile(SQL_PATH, buildSql(products), 'utf8');

  console.log(`Collected ${products.length} candidate products`);
  console.log(`Target import count: ${TARGET_COUNT}`);
  console.log(`JSON: ${JSON_PATH}`);
  console.log(`SQL: ${SQL_PATH}`);

  execFileSync('sqlcmd', ['-S', SQL_SERVER, '-d', SQL_DATABASE, '-E', '-C', '-b', '-f', '65001', '-i', SQL_PATH], {
    stdio: 'inherit',
  });
};

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
