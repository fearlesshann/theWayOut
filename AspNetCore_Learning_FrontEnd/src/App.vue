<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Plus, Refresh, Cloudy, Sunny, ColdDrink } from '@element-plus/icons-vue'

// 接口定义
interface WeatherForecast {
  id?: number
  date: string
  temperatureC: number
  temperatureF: number
  summary: string
}

interface PagedResult<T> {
  data: T[]
  total: number
  page: number
  pageSize: number
}

// 状态
const forecasts = ref<WeatherForecast[]>([])
const loading = ref(false)
const submitting = ref(false)

// 分页
const pagination = reactive({
  currentPage: 1,
  pageSize: 5,
  total: 0
})

// 表单
const formRef = ref()
const form = reactive({
  date: new Date().toISOString().split('T')[0],
  temperatureC: 25,
  summary: 'Sunny'
})

// 校验规则
const rules = {
  date: [{ required: true, message: 'Please pick a date', trigger: 'blur' }],
  temperatureC: [{ required: true, message: 'Temp is required', trigger: 'blur' }],
  summary: [{ required: true, message: 'Summary is required', trigger: 'blur' }]
}

const API_URL = '/weatherforecast'
const API_KEY = 'MySecretKey123'

// 获取数据
const fetchForecasts = async () => {
  loading.value = true
  try {
    const url = `${API_URL}?page=${pagination.currentPage}&pageSize=${pagination.pageSize}`
    const res = await fetch(url)
    if (!res.ok) throw new Error(`HTTP Error: ${res.status}`)
    
    const json: PagedResult<WeatherForecast> = await res.json()
    forecasts.value = json.data
    pagination.total = json.total
  } catch (err: any) {
    ElMessage.error('Failed to load data: ' + err.message)
  } finally {
    loading.value = false
  }
}

// 翻页
const handlePageChange = (page: number) => {
  pagination.currentPage = page
  fetchForecasts()
}

// 提交表单
const submitForm = async (formEl: any) => {
  if (!formEl) return
  await formEl.validate(async (valid: boolean) => {
    if (valid) {
      submitting.value = true
      try {
        const res = await fetch(API_URL, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-Api-Key': API_KEY
          },
          body: JSON.stringify(form)
        })
        
        if (!res.ok) throw new Error(await res.text())

        ElMessage.success('Forecast added successfully!')
        fetchForecasts() // 刷新列表
        
        // 重置表单但保留日期
        form.summary = ''
        form.temperatureC = Math.floor(Math.random() * 40) - 10
      } catch (err: any) {
        ElMessage.error('Submission failed: ' + err.message)
      } finally {
        submitting.value = false
      }
    }
  })
}

// 辅助函数：根据温度返回图标
const getWeatherIcon = (temp: number) => {
  if (temp > 30) return Sunny
  if (temp > 20) return Sunny
  if (temp < 0) return ColdDrink // 冰冻
  return Cloudy
}

// 辅助函数：根据温度返回 Tag 类型
const getTagType = (temp: number) => {
  if (temp > 30) return 'danger'
  if (temp > 20) return 'warning'
  if (temp < 0) return 'info'
  if (temp < 10) return ''
  return 'success'
}

onMounted(() => {
  fetchForecasts()
})
</script>

<template>
  <div class="app-container">
    <el-container>
      <el-header class="main-header">
        <div class="logo">
          <el-icon :size="32" color="#409EFF"><Cloudy /></el-icon>
          <h1>Weather<span style="color: #409EFF">Hub</span></h1>
        </div>
      </el-header>

      <el-main>
        <el-row :gutter="20">
          <!-- 左侧：添加表单 -->
          <el-col :xs="24" :md="8">
            <el-card class="box-card" shadow="hover">
              <template #header>
                <div class="card-header">
                  <span>Add Forecast</span>
                  <el-tag type="primary" effect="dark" size="small">New</el-tag>
                </div>
              </template>
              
              <el-form 
                ref="formRef" 
                :model="form" 
                :rules="rules" 
                label-position="top"
                size="large"
              >
                <el-form-item label="Date" prop="date">
                  <el-date-picker 
                    v-model="form.date" 
                    type="date" 
                    placeholder="Pick a date" 
                    style="width: 100%"
                    value-format="YYYY-MM-DD"
                  />
                </el-form-item>
                
                <el-form-item label="Temperature (°C)" prop="temperatureC">
                  <el-input-number 
                    v-model="form.temperatureC" 
                    :min="-100" 
                    :max="100" 
                    style="width: 100%"
                  />
                </el-form-item>
                
                <el-form-item label="Summary" prop="summary">
                  <el-input 
                    v-model="form.summary" 
                    placeholder="e.g. Scorching, Freezing..." 
                    :prefix-icon="Sunny"
                  />
                </el-form-item>

                <el-button 
                  type="primary" 
                  @click="submitForm(formRef)" 
                  :loading="submitting" 
                  style="width: 100%; margin-top: 10px;"
                  :icon="Plus"
                >
                  Submit Record
                </el-button>
              </el-form>
            </el-card>
          </el-col>

          <!-- 右侧：数据列表 -->
          <el-col :xs="24" :md="16">
            <el-card class="box-card" shadow="never">
              <template #header>
                <div class="card-header">
                  <span>Forecast History</span>
                  <el-button circle :icon="Refresh" @click="fetchForecasts" :loading="loading" />
                </div>
              </template>

              <el-table 
                :data="forecasts" 
                v-loading="loading" 
                style="width: 100%" 
                stripe
              >
                <el-table-column prop="date" label="Date" width="120" sortable />
                
                <el-table-column label="Temp" width="180">
                  <template #default="scope">
                    <el-tag :type="getTagType(scope.row.temperatureC)" effect="plain" round>
                      <div style="display: flex; align-items: center; gap: 5px;">
                        <component :is="getWeatherIcon(scope.row.temperatureC)" style="width: 14px;"/>
                        <b>{{ scope.row.temperatureC }}°C</b>
                      </div>
                    </el-tag>
                    <span style="color: #999; font-size: 12px; margin-left: 8px;">
                      {{ scope.row.temperatureF }}°F
                    </span>
                  </template>
                </el-table-column>
                
                <el-table-column prop="summary" label="Summary" />
              </el-table>

              <div class="pagination-container">
                <el-pagination
                  v-model:current-page="pagination.currentPage"
                  v-model:page-size="pagination.pageSize"
                  :total="pagination.total"
                  :page-sizes="[5, 10, 20]"
                  layout="total, sizes, prev, pager, next"
                  @current-change="handlePageChange"
                  @size-change="fetchForecasts"
                  background
                />
              </div>
            </el-card>
          </el-col>
        </el-row>
      </el-main>
    </el-container>
  </div>
</template>

<style scoped>
.app-container {
  max-width: 1200px;
  margin: 0 auto;
  min-height: 100vh;
}

.main-header {
  display: flex;
  align-items: center;
  border-bottom: 1px solid #dcdfe6;
  background-color: white;
}

.logo {
  display: flex;
  align-items: center;
  gap: 10px;
}

.logo h1 {
  font-size: 20px;
  margin: 0;
  color: #303133;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.pagination-container {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

/* 移动端适配 */
@media (max-width: 768px) {
  .el-col {
    margin-bottom: 20px;
  }
}
</style>
