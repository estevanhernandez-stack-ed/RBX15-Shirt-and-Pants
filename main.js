const { app, BrowserWindow } = require('electron')
const path = require('path')

function createWindow () {
  const win = new BrowserWindow({
    width: 1200,
    height: 800,
    title: "RBX15 Classic Shirt and Pants Maker",
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false
    }
  })

  // Load the main HTML file
  win.loadFile('editor.html')
  
  // Optionally open DevTools
  // win.webContents.openDevTools()
  
  // Remove the menu bar to make it look cleaner
  win.setMenuBarVisibility(false)
}

app.whenReady().then(() => {
  createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow()
    }
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})
