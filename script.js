/**
 * NekoGirl 网站交互脚本
 */

document.addEventListener('DOMContentLoaded', function() {
    // 初始化粒子效果
    initParticles();
    
    // 初始化滚动动画
    initScrollAnimations();
    
    // 初始化导航栏滚动效果
    initNavbarScroll();
    
    // 初始化平滑滚动
    initSmoothScroll();
});

/**
 * 粒子效果
 * 在英雄区域创建漂浮的粒子动画
 */
function initParticles() {
    const particlesContainer = document.getElementById('particles');
    if (!particlesContainer) return;
    
    const particleCount = 20;
    
    for (let i = 0; i < particleCount; i++) {
        createParticle(particlesContainer);
    }
}

function createParticle(container) {
    const particle = document.createElement('div');
    particle.className = 'particle';
    
    // 随机位置
    const left = Math.random() * 100;
    const delay = Math.random() * 20;
    const duration = 15 + Math.random() * 10;
    const size = 4 + Math.random() * 8;
    
    particle.style.left = `${left}%`;
    particle.style.animationDelay = `${delay}s`;
    particle.style.animationDuration = `${duration}s`;
    particle.style.width = `${size}px`;
    particle.style.height = `${size}px`;
    particle.style.opacity = 0.1 + Math.random() * 0.2;
    
    container.appendChild(particle);
}

/**
 * 滚动动画
 * 元素进入视口时触发动画
 */
function initScrollAnimations() {
    const animatedElements = document.querySelectorAll(
        '.feature-card, .preview-card, .shortcut-item, .requirement-card'
    );
    
    // 添加动画类
    animatedElements.forEach(el => {
        el.classList.add('animate-on-scroll');
    });
    
    // 创建观察器
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
                observer.unobserve(entry.target);
            }
        });
    }, {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    });
    
    // 观察所有元素
    animatedElements.forEach(el => observer.observe(el));
}

/**
 * 导航栏滚动效果
 * 滚动时改变导航栏样式
 */
function initNavbarScroll() {
    const navbar = document.querySelector('.navbar');
    if (!navbar) return;
    
    let lastScroll = 0;
    
    window.addEventListener('scroll', () => {
        const currentScroll = window.pageYOffset;
        
        // 添加/移除阴影
        if (currentScroll > 10) {
            navbar.style.boxShadow = '0 4px 20px rgba(0, 0, 0, 0.08)';
        } else {
            navbar.style.boxShadow = 'none';
        }
        
        lastScroll = currentScroll;
    });
}

/**
 * 平滑滚动
 * 点击导航链接时平滑滚动到目标区域
 */
function initSmoothScroll() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            e.preventDefault();
            
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;
            
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                const navHeight = document.querySelector('.navbar').offsetHeight;
                const targetPosition = targetElement.offsetTop - navHeight - 20;
                
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });
}

/**
 * 图片懒加载
 * 延迟加载视口外的图片
 */
function initLazyLoading() {
    const images = document.querySelectorAll('img[data-src]');
    
    const imageObserver = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const img = entry.target;
                img.src = img.dataset.src;
                img.removeAttribute('data-src');
                imageObserver.unobserve(img);
            }
        });
    });
    
    images.forEach(img => imageObserver.observe(img));
}

/**
 * 按钮点击效果
 * 添加涟漪动画
 */
document.querySelectorAll('.btn').forEach(button => {
    button.addEventListener('click', function(e) {
        const rect = this.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;
        
        const ripple = document.createElement('span');
        ripple.style.cssText = `
            position: absolute;
            background: rgba(255, 255, 255, 0.3);
            border-radius: 50%;
            transform: scale(0);
            animation: ripple 0.6s ease-out;
            pointer-events: none;
            left: ${x}px;
            top: ${y}px;
            width: 100px;
            height: 100px;
            margin-left: -50px;
            margin-top: -50px;
        `;
        
        this.style.position = 'relative';
        this.style.overflow = 'hidden';
        this.appendChild(ripple);
        
        setTimeout(() => ripple.remove(), 600);
    });
});

// 添加涟漪动画样式
const style = document.createElement('style');
style.textContent = `
    @keyframes ripple {
        to {
            transform: scale(4);
            opacity: 0;
        }
    }
`;
document.head.appendChild(style);

/**
 * 预览图片放大效果
 */
document.querySelectorAll('.preview-card').forEach(card => {
    const img = card.querySelector('.preview-image');
    if (!img) return;
    
    card.addEventListener('mouseenter', () => {
        img.style.transform = 'scale(1.05)';
    });
    
    card.addEventListener('mouseleave', () => {
        img.style.transform = 'scale(1)';
    });
});

/**
 * 键盘快捷键提示
 * 在页面加载后短暂显示快捷键提示
 */
function showShortcutHint() {
    const hint = document.createElement('div');
    hint.style.cssText = `
        position: fixed;
        bottom: 24px;
        right: 24px;
        background: var(--neko-pink-gradient);
        color: white;
        padding: 16px 20px;
        border-radius: 12px;
        font-size: 14px;
        box-shadow: 0 8px 32px rgba(255, 107, 157, 0.4);
        z-index: 9999;
        animation: slideIn 0.5s ease;
    `;
    hint.innerHTML = `
        <div style="font-weight: 600; margin-bottom: 4px;">💡 快捷键提示</div>
        <div style="opacity: 0.9;">使用 ← → 方向键快速导航</div>
    `;
    
    document.body.appendChild(hint);
    
    setTimeout(() => {
        hint.style.animation = 'slideOut 0.5s ease forwards';
        setTimeout(() => hint.remove(), 500);
    }, 5000);
}

// 添加快捷键提示动画
const hintStyle = document.createElement('style');
hintStyle.textContent = `
    @keyframes slideIn {
        from {
            transform: translateX(100%);
            opacity: 0;
        }
        to {
            transform: translateX(0);
            opacity: 1;
        }
    }
    @keyframes slideOut {
        from {
            transform: translateX(0);
            opacity: 1;
        }
        to {
            transform: translateX(100%);
            opacity: 0;
        }
    }
`;
document.head.appendChild(hintStyle);

// 页面加载完成后显示提示
window.addEventListener('load', () => {
    setTimeout(showShortcutHint, 2000);
});
