let angle = 0;
let squares = [];

function setup() {
  // 建立一個和瀏覽器視窗一樣大的畫布
  let canvas = createCanvas(windowWidth, windowHeight);
  
  // 將這個畫布放到 id 為 'p5-background' 的容器裡
  canvas.parent('p5-background');
  
  rectMode(CENTER);
  colorMode(HSB, 360, 100, 100, 100);
  noStroke();

  for (let i = 0; i < 50; i++) {
    squares.push(new BouncingSquare());
  }
}

// 當視窗大小改變時，自動調整畫布大小
function windowResized() {
  resizeCanvas(windowWidth, windowHeight);
}

function draw() {
  // 使用深色背景以匹配網站風格 HSB(色相, 飽和度, 亮度)
  background(21, 100, 15, 100); 

  // --- 中央的脈衝方塊 ---
  let centerSize = map(sin(angle), -1, 1, 100, 250);
  let centerHue = map(sin(angle), -1, 1, 15, 35); // 色相調整為橘色系

  // 外層模糊方塊
  fill(centerHue, 90, 90, 15); // 透明度調低，更融入背景
  push();
  translate(width / 2, height / 2);
  rotate(angle / 2);
  rect(0, 0, centerSize, centerSize, 10);
  pop();

  // 內層實心方塊
  fill(centerHue, 90, 90, 50);
  push();
  translate(width / 2, height / 2);
  rotate(angle / 2);
  rect(0, 0, centerSize * 0.9, centerSize * 0.9, 8);
  pop();

  // --- 四處移動的小方塊 ---
  for (let sq of squares) {
    sq.update();
    sq.display();
  }

  angle += 0.015;
}

// --- BouncingSquare Class 的定義 ---
class BouncingSquare {
  constructor() {
    this.pos = createVector(random(width), random(height));
    this.vel = p5.Vector.random2D().mult(random(1, 3));
    this.size = random(10, 40);
    this.rotation = random(TWO_PI);
    this.rotationSpeed = random(-0.05, 0.05);
    // 色相調整為橘黃色系，以匹配網站
    this.hue = random(15, 50); 
    this.sat = random(70, 100);
    this.bri = random(80, 100);
  }

  update() {
    this.pos.add(this.vel);
    this.rotation += this.rotationSpeed;

    // 邊界碰撞反彈
    if (this.pos.x < this.size / 2 || this.pos.x > width - this.size / 2) {
      this.vel.x *= -1;
    }
    if (this.pos.y < this.size / 2 || this.pos.y > height - this.size / 2) {
      this.vel.y *= -1;
    }
  }

  display() {
    push();
    translate(this.pos.x, this.pos.y);
    rotate(this.rotation);
    fill(this.hue, this.sat, this.bri, 30); // 透明度降低，讓效果更微妙
    rect(0, 0, this.size, this.size, 4);
    pop();
  }
}